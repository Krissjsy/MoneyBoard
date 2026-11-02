import { expect } from '@playwright/test'

export default async function globalSetup() {
  await expect.poll(async () => {
    try {
      const response = await fetch('http://localhost:5080/api/health')
      return response.ok
    } catch { return false }
  }, { timeout: 45_000, message: 'MoneyBoard API did not become healthy' }).toBe(true)
}
