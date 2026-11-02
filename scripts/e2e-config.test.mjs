import test from 'node:test'
import assert from 'node:assert/strict'
import { createServer } from 'node:net'
import { assertE2EWebPortAvailable, E2E_WEB_PORT, validateE2EWebPort } from './e2e-config.mjs'

test('uses the fixed Playwright port and rejects a conflicting development override', () => {
  assert.equal(validateE2EWebPort({}), E2E_WEB_PORT)
  assert.equal(validateE2EWebPort({ MONEYBOARD_WEB_PORT: E2E_WEB_PORT }), E2E_WEB_PORT)
  assert.throws(() => validateE2EWebPort({ MONEYBOARD_WEB_PORT: '5174' }), /E2E tests require MONEYBOARD_WEB_PORT=5173/)
})

test('reports an occupied E2E web port before starting services', async () => {
  const listener = createServer()
  await new Promise((resolve, reject) => {
    listener.once('error', reject)
    listener.listen(0, 'localhost', resolve)
  })
  try {
    const address = listener.address()
    assert.ok(address && typeof address === 'object')
    await assert.rejects(assertE2EWebPortAvailable(String(address.port)), /already in use/)
  } finally {
    await new Promise(resolve => listener.close(resolve))
  }
})
