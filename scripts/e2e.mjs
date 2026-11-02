import { spawn, spawnSync } from 'node:child_process'
import { createServer as createHttpServer } from 'node:http'
import { createServer as createNetServer } from 'node:net'
import { randomBytes } from 'node:crypto'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { e2eSafetyConstants, validateE2EConfiguration } from './e2e-safety.mjs'
import { reportBootstrapError, runWithLock, stopOwnedChildren, waitForLockAcquisition } from './e2e-lock.mjs'
import { assertE2EWebPortAvailable, E2E_WEB_PORT, E2E_WEB_URL, validateE2EWebPort } from './e2e-config.mjs'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const { DATABASE_NAME } = e2eSafetyConstants
const webPort = validateE2EWebPort(process.env)
const markerName = 'MoneyBoard E2E isolated database'
const markerValue = 'moneyboard-e2e-local-only-v1'
const connection = validateE2EConfiguration(process.env)
const signingKey = randomBytes(48).toString('base64url')
if (Buffer.byteLength(signingKey, 'utf8') < 32) throw new Error('Could not create a valid ephemeral E2E signing key.')

// This exact validated connection is passed to both EF tooling and the API process.
const env = {
  ...process.env,
  ConnectionStrings__MoneyBoard: connection.connectionString,
  Authentication__SigningKey: signingKey,
  Authentication__Issuer: 'MoneyBoard.E2E',
  Authentication__Audience: 'MoneyBoard.E2E',
  Frontend__Origin: E2E_WEB_URL,
  Frontend__BaseUrl: E2E_WEB_URL,
  Email__SmtpHost: '127.0.0.1',
  Email__SmtpPort: '1025',
  Email__EnableSsl: 'false',
  Email__From: 'MoneyBoard E2E <e2e@moneyboard.invalid>',
  Email__Username: '',
  Email__Password: '',
  MONEYBOARD_WEB_PORT: webPort,
  ASPNETCORE_ENVIRONMENT: 'E2E',
  DOTNET_ENVIRONMENT: 'E2E',
  Logging__LogLevel__Default: 'Warning',
}

const activeChildren = new Set()
const servers = new Set()
const sockets = new Set()
let stopping = false
let cleanupPromise
let signalExitCode = 0

function waitForClose(child, timeoutMs) {
  if (child.exitCode !== null || child.signalCode !== null) return Promise.resolve(true)
  return new Promise(resolve => {
    const timer = setTimeout(() => finish(false), timeoutMs)
    const finish = closed => {
      clearTimeout(timer)
      child.off('close', onClose)
      resolve(closed)
    }
    const onClose = () => finish(true)
    child.once('close', onClose)
  })
}

async function terminate(child, force = false) {
  if (!child.pid || child.exitCode !== null || child.signalCode !== null) return
  if (process.platform === 'win32') {
    if (force) {
      // /T is scoped to this child PID and its descendants, never a process name.
      spawnSync('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore', windowsHide: true })
    } else child.kill('SIGTERM')
  } else {
    try { process.kill(force ? -child.pid : child.pid, force ? 'SIGKILL' : 'SIGTERM') } catch { /* Already exited. */ }
  }
}

async function run(file, args, environment = env) {
  if (stopping) throw new Error('E2E bootstrap was interrupted.')
  const child = spawn(file, args, {
    cwd: root,
    env: environment,
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
    detached: process.platform !== 'win32',
  })
  activeChildren.add(child)
  let stdout = ''
  let stderr = ''
  child.stdout.setEncoding('utf8').on('data', chunk => { stdout += chunk })
  child.stderr.setEncoding('utf8').on('data', chunk => { stderr += chunk })
  const result = await new Promise((resolve, reject) => {
    child.once('error', reject)
    child.once('close', (code, signal) => resolve({ code, signal }))
  }).finally(() => activeChildren.delete(child))
  if (result.code !== 0) {
    if (stopping) throw new Error('E2E bootstrap was interrupted.')
    const diagnostic = `${stdout}\n${stderr}`.trim().slice(-2000)
    throw new Error(`E2E preflight command failed (${path.basename(file)}).${diagnostic ? `\n${diagnostic}` : ''}`)
  }
  return stdout.trim()
}

function sql(query, database = 'master') {
  return run('sqlcmd', ['-S', '.', '-E', '-C', '-d', database, '-b', '-h', '-1', '-W', '-s', '|', '-Q', query])
}

function localIdentity(row) {
  const [machine, instance, database] = row.split('|')
  const localHost = os.hostname().split('.')[0].toLowerCase()
  if (!machine || machine.split('.')[0].toLowerCase() !== localHost || instance !== '<default>' || database !== 'master')
    throw new Error('Connected SQL Server identity is not this machine’s local default instance; refusing database access.')
}

function verifyTargetIdentity(row) {
  const [machine, instance, database, marker] = row.split('|')
  const localHost = os.hostname().split('.')[0].toLowerCase()
  if (!machine || machine.split('.')[0].toLowerCase() !== localHost || instance !== '<default>' || database !== DATABASE_NAME)
    throw new Error('Connected database identity does not match the validated local E2E target; refusing migration.')
  if (marker !== 'VERIFIED')
    throw new Error('The E2E database safety marker is missing or invalid; refusing migration without repairing it.')
}

const serverIdentityQuery = `SET NOCOUNT ON; SELECT CONCAT(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'|', COALESCE(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'<default>'), N'|', DB_NAME(), N'|', CASE WHEN DB_ID(N'${DATABASE_NAME}') IS NULL THEN N'ABSENT' ELSE N'EXISTS' END);`
const targetIdentityQuery = `SET NOCOUNT ON; SELECT CONCAT(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'|', COALESCE(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'<default>'), N'|', DB_NAME(), N'|', CASE WHEN OBJECT_ID(N'dbo.MoneyBoardE2ESafetyMarker', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.MoneyBoardE2ESafetyMarker WHERE MarkerName=N'${markerName}' AND MarkerValue=N'${markerValue}') THEN N'VERIFIED' ELSE N'UNMARKED' END);`

async function acquireSqlApplicationLock() {
  const query = `SET NOCOUNT ON; DECLARE @lockResult int; EXEC @lockResult = sys.sp_getapplock @Resource=N'MoneyBoard_E2E bootstrap', @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=0, @DbPrincipal=N'public'; IF @lockResult < 0 THROW 51000, 'Another E2E bootstrap already holds the SQL application lock.', 1; PRINT N'MONEYBOARD_E2E_LOCK_ACQUIRED'; WHILE 1=1 WAITFOR DELAY '00:10:00';`
  const child = spawn('sqlcmd', ['-S', '.', '-E', '-C', '-d', 'master', '-b', '-h', '-1', '-Q', query], {
    cwd: root,
    env,
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
    detached: process.platform !== 'win32',
  })
  activeChildren.add(child)
  child.once('close', () => activeChildren.delete(child))
  return waitForLockAcquisition(child, { token: 'MONEYBOARD_E2E_LOCK_ACQUIRED' })
}

async function ensureMarkedDatabase() {
  const serverRow = await sql(serverIdentityQuery)
  const identityLine = serverRow.split(/\r?\n/).map(line => line.trim()).filter(Boolean).at(-1) ?? ''
  const [machine, instance, currentDatabase, state] = identityLine.split('|')
  localIdentity(`${machine}|${instance}|${currentDatabase}`)

  if (state === 'ABSENT') {
    if (process.env.MONEYBOARD_E2E_ALLOW_CREATE !== 'I_CREATE_MONEYBOARD_E2E')
      throw new Error(`The ${DATABASE_NAME} database does not exist. Set MONEYBOARD_E2E_ALLOW_CREATE=I_CREATE_MONEYBOARD_E2E once to create and mark this dedicated test database.`)
    // The target is absent, the server identity is verified, and creation requires a separate explicit opt-in.
    await sql(`CREATE DATABASE [${DATABASE_NAME}];`, 'master')
    await sql(`CREATE TABLE dbo.MoneyBoardE2ESafetyMarker (MarkerName nvarchar(100) NOT NULL PRIMARY KEY, MarkerValue nvarchar(100) NOT NULL); INSERT INTO dbo.MoneyBoardE2ESafetyMarker (MarkerName, MarkerValue) VALUES (N'${markerName}', N'${markerValue}');`, DATABASE_NAME)
  } else if (state !== 'EXISTS') {
    throw new Error('Could not establish whether the dedicated E2E database exists; refusing database modification.')
  }

  const targetRow = await sql(targetIdentityQuery, DATABASE_NAME)
  const targetLine = targetRow.split(/\r?\n/).map(line => line.trim()).filter(Boolean).at(-1) ?? ''
  verifyTargetIdentity(targetLine)
}

function createSmtpServer(messages) {
  return createNetServer(socket => {
    sockets.add(socket)
    socket.once('close', () => sockets.delete(socket))
    socket.setEncoding('utf8')
    socket.on('error', () => {})
    socket.write('220 localhost MoneyBoard E2E SMTP\r\n')
    let buffer = ''
    let dataMode = false
    let message = []
    socket.on('data', chunk => {
      buffer += chunk
      while (buffer.includes('\n')) {
        const end = buffer.indexOf('\n') + 1
        const line = buffer.slice(0, end).replace(/\r?\n$/, '')
        buffer = buffer.slice(end)
        if (dataMode) {
          if (line === '.') {
            dataMode = false
            messages.push(message.join('\r\n'))
            message = []
            socket.write('250 2.0.0 Message accepted\r\n')
          } else message.push(line.startsWith('..') ? line.slice(1) : line)
        } else if (/^(EHLO|HELO)\b/i.test(line)) socket.write('250-localhost\r\n250 SIZE 15728640\r\n')
        else if (/^DATA\b/i.test(line)) { dataMode = true; socket.write('354 End data with <CR><LF>.<CR><LF>\r\n') }
        else if (/^(MAIL FROM|RCPT TO|RSET|NOOP)\b/i.test(line)) socket.write('250 2.1.0 OK\r\n')
        else if (/^QUIT\b/i.test(line)) { socket.write('221 2.0.0 Bye\r\n'); socket.end() }
        else socket.write('250 2.0.0 OK\r\n')
      }
    })
  })
}

function createMailboxServer(messages) {
  return createHttpServer((request, response) => {
    if (request.method === 'POST' && request.url === '/messages/clear') {
      messages.length = 0
      response.writeHead(204).end()
    } else if (request.method === 'GET' && request.url === '/messages/latest') {
      response.writeHead(messages.length ? 200 : 404, { 'Content-Type': 'application/json' })
      response.end(JSON.stringify({ message: messages.at(-1) ?? '' }))
    } else response.writeHead(404).end()
  })
}

function listen(server, port, host) {
  if (stopping) return Promise.reject(new Error('E2E bootstrap was interrupted.'))
  servers.add(server)
  return new Promise((resolve, reject) => {
    const onError = error => { server.off('listening', onListening); reject(error) }
    const onListening = () => {
      server.off('error', onError)
      if (stopping) { server.close(); reject(new Error('E2E bootstrap was interrupted.')) }
      else resolve()
    }
    server.once('error', onError)
    server.once('listening', onListening)
    server.listen(port, host)
  })
}

function cleanup() {
  if (cleanupPromise) return cleanupPromise
  stopping = true
  cleanupPromise = (async () => {
    for (const server of servers) if (server.listening) server.close()
    for (const socket of sockets) socket.destroy()
    // Windows requires terminating each owned process tree; POSIX gets a
    // graceful signal first and escalates only for children that remain open.
    await stopOwnedChildren(activeChildren, { platform: process.platform, terminate, waitForClose })
  })()
  return cleanupPromise
}

async function main() {
  await assertE2EWebPortAvailable(E2E_WEB_PORT)
  const lock = await acquireSqlApplicationLock()
  return runWithLock(lock, async () => {
    await ensureMarkedDatabase()
    await run('dotnet', ['tool', 'restore'])
    await run('dotnet', [
      'tool', 'run', 'dotnet-ef', '--', 'database', 'update',
      '--project', path.join(root, 'backend', 'MoneyBoard.Api', 'MoneyBoard.Api.csproj'),
    ])

    const messages = []
    await listen(createSmtpServer(messages), 1025, '127.0.0.1')
    await listen(createMailboxServer(messages), 5081, '127.0.0.1')
    if (stopping) throw new Error('E2E bootstrap was interrupted.')
    console.log('Started loopback-only SMTP capture for password recovery.')

    const application = spawn(process.execPath, [path.join(root, 'scripts', 'dev.mjs')], {
      cwd: root,
      env,
      stdio: 'inherit',
      windowsHide: true,
      detached: process.platform !== 'win32',
    })
    activeChildren.add(application)
    try {
      const result = await new Promise((resolve, reject) => {
        application.once('error', reject)
        application.once('close', (code, signal) => resolve({ code, signal }))
      })
      if (stopping) return signalExitCode
      return result.code ?? (result.signal ? 1 : 0)
    } finally {
      activeChildren.delete(application)
    }
  }, cleanup)
}

function handleSignal(signal) {
  signalExitCode = signal === 'SIGINT' ? 130 : 143
  void cleanup()
}

process.once('SIGINT', () => handleSignal('SIGINT'))
process.once('SIGTERM', () => handleSignal('SIGTERM'))

let exitCode = 1
try {
  exitCode = await main()
} catch (error) {
  if (!stopping) reportBootstrapError(error)
  else exitCode = signalExitCode || 1
} finally {
  await cleanup()
}
process.exitCode = exitCode
