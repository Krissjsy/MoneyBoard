import { createServer } from 'node:net'

export const E2E_WEB_PORT = '5173'
export const E2E_WEB_URL = `http://localhost:${E2E_WEB_PORT}`

export function validateE2EWebPort(environment) {
  const configuredPort = environment.MONEYBOARD_WEB_PORT
  if (configuredPort && configuredPort !== E2E_WEB_PORT)
    throw new Error(`E2E tests require MONEYBOARD_WEB_PORT=${E2E_WEB_PORT}; remove the unsupported override or set it to ${E2E_WEB_PORT}.`)
  return E2E_WEB_PORT
}

export function assertE2EWebPortAvailable(port = E2E_WEB_PORT) {
  const server = createServer()
  return new Promise((resolve, reject) => {
    let settled = false
    const finish = error => {
      if (settled) return
      settled = true
      server.off('error', onError)
      if (error) reject(error)
      else server.close(closeError => closeError ? reject(closeError) : resolve())
    }
    const onError = error => {
      if (error.code === 'EADDRINUSE') finish(new Error(`E2E web port ${port} is already in use; free that port before starting browser tests.`))
      else finish(new Error(`Could not check E2E web port ${port}: ${error.message}`))
    }
    server.once('error', onError)
    server.listen(Number(port), 'localhost', () => finish())
  })
}
