/**
 * Wait for an owned SQL lock process to confirm acquisition and keep observing
 * it for the lifetime of the protected operation.
 */
export function reportBootstrapError(error, write = console.error) {
  write(error instanceof Error ? error.message : 'E2E bootstrap failed.')
}

export function waitForLockAcquisition(child, { token, timeoutMs = 15_000 }) {
  return new Promise((resolve, reject) => {
    let settled = false
    let acquired = false
    let alive = true
    let output = ''
    let errors = ''
    let resolveLost
    const lost = new Promise(resolveLoss => { resolveLost = resolveLoss })
    const timeoutDiagnostic = () => {
      const details = []
      const stdoutTail = output.trim().slice(-1000)
      const stderrTail = errors.trim().slice(-1000)
      details.push(`sqlcmd stderr (last 1000 characters):\n${stderrTail || '<no output received>'}`)
      details.push(`sqlcmd stdout (last 1000 characters):\n${stdoutTail || '<no output received>'}`)
      return `\n${details.join('\n')}`
    }
    const timer = setTimeout(() => finish(new Error(
      `Timed out acquiring the local SQL E2E application lock.${timeoutDiagnostic()}`,
    )), timeoutMs)

    const finish = error => {
      if (settled) return
      settled = true
      clearTimeout(timer)
      if (error) reject(error)
      else resolve({
        child,
        lost,
        get alive() { return alive },
      })
    }

    child.stdout.setEncoding?.('utf8')
    child.stderr.setEncoding?.('utf8')
    child.stdout.on('data', chunk => {
      output += chunk
      if (!acquired && output.includes(token)) {
        acquired = true
        finish()
      }
    })
    child.stderr.on('data', chunk => { errors += chunk })
    child.once('error', error => {
      alive = false
      const failure = new Error(`Could not start or retain the SQL E2E lock session: ${error.message}`)
      if (!acquired) finish(failure)
      else resolveLost(failure)
    })
    child.once('close', (code, signal) => {
      alive = false
      const detail = errors.trim().slice(-500)
      const failure = new Error(acquired
        ? `The SQL E2E application-lock session exited unexpectedly (code ${code ?? 'null'}, signal ${signal ?? 'none'}).${detail ? ` ${detail}` : ''}`
        : `Could not acquire the SQL E2E application lock.${detail ? ` ${detail}` : ` sqlcmd exited with code ${code}.`}`)
      if (!acquired) finish(failure)
      else resolveLost(failure)
    })
  })
}

/** Race the whole protected bootstrap against loss of the lock session. */
export async function runWithLock(lock, operation, stopOwned) {
  let operationError
  try {
    const lockLost = lock.lost.then(error => { throw error })
    const protectedWork = Promise.resolve().then(() => {
      // The acquisition event and process close can arrive back-to-back. Check
      // liveness again immediately before invoking schema-changing work.
      if (!lock.alive) return lock.lost.then(error => { throw error })
      return operation()
    })
    const result = await Promise.race([protectedWork, lockLost])
    if (!lock.alive) throw new Error('The SQL E2E application lock was lost before the protected operation completed.')
    return result
  } catch (error) {
    operationError = error
    try {
      await stopOwned()
    } catch (cleanupError) {
      // Keep the failure that stopped the bootstrap as the primary error.
      if (operationError && typeof operationError === 'object') operationError.cleanupError = cleanupError
    }
    throw operationError
  }
}

/** Stop only the processes present in the runner's owned-child registry. */
export async function stopOwnedChildren(ownedChildren, { platform, terminate, waitForClose }) {
  const children = [...ownedChildren]
  if (platform === 'win32') {
    for (const child of children) await terminate(child, true)
    for (const child of children) await waitForClose(child, 4_000)
  } else {
    for (const child of children) await terminate(child)
    for (const child of children) {
      if (!await waitForClose(child, 4_000)) await terminate(child, true)
    }
  }
}
