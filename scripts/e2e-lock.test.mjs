import test from 'node:test'
import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { reportBootstrapError, waitForLockAcquisition, runWithLock, stopOwnedChildren } from './e2e-lock.mjs'

const token = 'MONEYBOARD_E2E_LOCK_ACQUIRED'

function fakeChild() {
  const child = new EventEmitter()
  child.stdout = new EventEmitter()
  child.stdout.setEncoding = () => child.stdout
  child.stderr = new EventEmitter()
  child.stderr.setEncoding = () => child.stderr
  return child
}

async function acquiredChild() {
  const child = fakeChild()
  const lockPromise = waitForLockAcquisition(child, { token, timeoutMs: 100 })
  child.stdout.emit('data', token)
  return { child, lock: await lockPromise }
}

test('accepts an explicit successful lock acquisition and completes while session stays alive', async () => {
  const { lock } = await acquiredChild()
  assert.equal(lock.alive, true)
  assert.equal(await runWithLock(lock, async () => 'complete', async () => {}), 'complete')
})

test('rejects lock acquisition failure before protected work', async () => {
  const child = fakeChild()
  const pending = waitForLockAcquisition(child, { token, timeoutMs: 100 })
  child.stderr.emit('data', 'application lock is held')
  child.emit('close', 1, null)
  await assert.rejects(pending, /Could not acquire/)
})

test('rejects lock acquisition timeout', async () => {
  const child = fakeChild()
  await assert.rejects(waitForLockAcquisition(child, { token, timeoutMs: 5 }), /Timed out acquiring/)
})

test('includes captured sqlcmd output when lock acquisition times out', async () => {
  const child = fakeChild()
  const pending = waitForLockAcquisition(child, { token, timeoutMs: 5 })
  child.stdout.emit('data', 'Connecting to local SQL Server...')
  child.stderr.emit('data', 'Login failed for the current Windows account.')

  await assert.rejects(pending, error => {
    const terminalOutput = []
    reportBootstrapError(error, message => terminalOutput.push(message))
    const terminalMessage = terminalOutput.join('\n')
    assert.match(terminalMessage, /Timed out acquiring the local SQL E2E application lock/)
    assert.match(terminalMessage, /sqlcmd stderr .*\nLogin failed for the current Windows account\./)
    assert.match(terminalMessage, /sqlcmd stdout .*\nConnecting to local SQL Server/)
    return true
  })
})

test('runner error formatting labels both streams when either has no output', async () => {
  for (const { stream, message, emptyStream } of [
    { stream: 'stderr', message: 'SQL diagnostic on stderr', emptyStream: 'stdout' },
    { stream: 'stdout', message: 'SQL diagnostic on stdout', emptyStream: 'stderr' },
  ]) {
    const child = fakeChild()
    const pending = waitForLockAcquisition(child, { token, timeoutMs: 5 })
    child[stream].emit('data', message)

    await assert.rejects(pending, error => {
      const terminalOutput = []
      reportBootstrapError(error, message => terminalOutput.push(message))
      const terminalMessage = terminalOutput.join('\n')
      assert.ok(terminalMessage.includes(message))
      assert.match(terminalMessage, new RegExp(`sqlcmd ${emptyStream} .*\\n<no output received>`))
      return true
    })
  }
})

test('runner error formatting labels both streams when sqlcmd emitted nothing', async () => {
  const child = fakeChild()
  const pending = waitForLockAcquisition(child, { token, timeoutMs: 5 })

  await assert.rejects(pending, error => {
    const terminalOutput = []
    reportBootstrapError(error, message => terminalOutput.push(message))
    const terminalMessage = terminalOutput.join('\n')
    assert.match(terminalMessage, /sqlcmd stderr .*\n<no output received>/)
    assert.match(terminalMessage, /sqlcmd stdout .*\n<no output received>/)
    return true
  })
})

test('does not start migrations if the acquired lock session exits before work begins', async () => {
  const { child, lock } = await acquiredChild()
  child.emit('close', 0, null)
  let started = false
  await assert.rejects(runWithLock(lock, async () => { started = true }, async () => {}), /exited unexpectedly/)
  assert.equal(started, false)
})

test('stops owned work when the lock session exits during migration', async () => {
  const { child, lock } = await acquiredChild()
  let finishMigration
  const migration = new Promise(resolve => { finishMigration = resolve })
  let stopped = false
  const running = runWithLock(lock, () => migration, async () => { stopped = true })
  child.emit('close', 0, null)
  await assert.rejects(running, /exited unexpectedly/)
  assert.equal(stopped, true)
  finishMigration()
})

test('preserves migration failure when cleanup also fails', async () => {
  const { lock } = await acquiredChild()
  const migrationError = new Error('migration failed')
  await assert.rejects(
    runWithLock(lock, async () => { throw migrationError }, async () => { throw new Error('cleanup failed') }),
    error => error === migrationError && error.cleanupError.message === 'cleanup failed',
  )
})

test('treats a competing bootstrap rejection as contention and never starts migrations', async () => {
  const child = fakeChild()
  const pending = waitForLockAcquisition(child, { token, timeoutMs: 100 })
  child.stderr.emit('data', 'Another E2E bootstrap already holds the SQL application lock.')
  child.emit('close', 1, null)
  await assert.rejects(pending, /Could not acquire/)
})

test('cleanup terminates only processes registered as owned', async () => {
  const owned = { stopped: false }
  const unrelated = { stopped: false }
  const terminated = []
  await stopOwnedChildren(new Set([owned]), {
    platform: 'win32',
    terminate: async child => { child.stopped = true; terminated.push(child) },
    waitForClose: async () => true,
  })
  assert.deepEqual(terminated, [owned])
  assert.equal(unrelated.stopped, false)
})
