import { defineConfig, devices } from '@playwright/test'
import { E2E_WEB_URL, validateE2EWebPort } from './scripts/e2e-config.mjs'

validateE2EWebPort(process.env)
const webUrl = E2E_WEB_URL

export default defineConfig({
  testDir: './tests/e2e',
  globalSetup: './tests/e2e/global-setup.ts',
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  expect: { timeout: 8_000 },
  reporter: [['list'], ['html', { outputFolder: 'playwright-report', open: 'never' }]],
  outputDir: 'test-results',
  retries: 0,
  use: {
    ...devices['Desktop Chrome'],
    channel: 'chrome',
    baseURL: webUrl,
    headless: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'node scripts/e2e.mjs',
    url: webUrl,
    timeout: 180_000,
    reuseExistingServer: false,
    stdout: 'pipe',
    stderr: 'pipe',
  },
})
