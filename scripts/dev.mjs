import { spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const isWindows = process.platform === 'win32'
const children = []
let stopping = false
const webPort = process.env.MONEYBOARD_WEB_PORT ?? '5173'
if (!/^\d+$/.test(webPort) || Number(webPort) < 1 || Number(webPort) > 65535) {
  throw new Error('MONEYBOARD_WEB_PORT must be a valid TCP port number.')
}
const frontendOrigin = `http://localhost:${webPort}`

function start(name, command, args, options = {}) {
  const child = spawn(command, args, {
    cwd: root,
    env: { ...process.env, ...options.env },
    stdio: 'inherit',
    windowsHide: true,
  })
  children.push({ name, child })
  child.on('error', error => {
    console.error(`[${name}] failed to start: ${error.message}`)
    stop(1)
  })
  child.on('exit', (code, signal) => {
    if (!stopping) {
      console.error(`[${name}] exited (${signal ?? code ?? 'unknown'}); stopping the other development process.`)
      stop(code || 1)
    }
  })
  return child
}

function stop(exitCode = 0) {
  if (stopping) return
  stopping = true
  for (const { child } of children) {
    if (child.exitCode === null && child.signalCode === null) child.kill(isWindows ? undefined : 'SIGTERM')
  }
  const pending = children.map(({ child }) => new Promise(resolve => {
    if (child.exitCode !== null || child.signalCode !== null) resolve()
    else child.once('exit', resolve)
  }))
  Promise.all(pending).finally(() => process.exit(exitCode))
  setTimeout(() => process.exit(exitCode), 5000).unref()
}

process.on('SIGINT', () => stop(0))
process.on('SIGTERM', () => stop(0))

console.log(`Starting MoneyBoard API at http://localhost:5080 and frontend at ${frontendOrigin}`)
start('api', 'dotnet', [
  'run', '--project', path.join(root, 'backend', 'MoneyBoard.Api', 'MoneyBoard.Api.csproj'),
  '--no-launch-profile', '--urls', 'http://localhost:5080',
], { env: { ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', Frontend__Origin: frontendOrigin } })
start('web', process.execPath, [path.join(root, 'node_modules', 'vite', 'bin', 'vite.js'), '--host', 'localhost', '--port', webPort, '--strictPort'], { env: { MONEYBOARD_WEB_PORT: webPort } })
