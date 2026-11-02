import { test as base, expect, type Page } from '@playwright/test'
import { randomUUID } from 'node:crypto'

type Account = { name: string; email: string; password: string }

export const test = base.extend<{ monitoredPage: Page }>({
  monitoredPage: async ({ page }, use, testInfo) => {
    const diagnostics = { consoleErrors: [] as { text: string; source: string }[], pageErrors: [] as string[], failedRequests: [] as string[], httpFailures: [] as { status: number; method: string; path: string }[], apiFailures: [] as { status: number; path: string; expected: boolean }[] }
    page.on('console', message => {
      if (message.type() === 'error') {
        let source = message.location().url
        try { source = new URL(source).pathname } catch { source = source ? '<non-http source>' : '<no source>' }
        diagnostics.consoleErrors.push({ text: message.text(), source })
      }
    })
    page.on('pageerror', error => diagnostics.pageErrors.push(error.message))
    page.on('requestfailed', request => diagnostics.failedRequests.push(`${request.method()} ${new URL(request.url()).pathname} (${request.failure()?.errorText ?? 'failed'})`))
    page.on('response', response => {
      const path = new URL(response.url()).pathname
      if (response.status() >= 400) diagnostics.httpFailures.push({ status: response.status(), method: response.request().method(), path })
      if (path.startsWith('/api/') && response.status() >= 400) diagnostics.apiFailures.push({
        status: response.status(),
        path,
        expected: testInfo.title === 'an API failure while saving onboarding progress is shown to the user' && path === '/api/setup/progress' && response.status() === 500,
      })
    })
    await use(page)
    const expectedApiFailures = diagnostics.apiFailures.filter(item => item.expected).length
    const expectedSynthetic500 = testInfo.title === 'an API failure while saving onboarding progress is shown to the user'
    const unexpectedConsoleErrors = diagnostics.consoleErrors.filter(message => !(expectedSynthetic500 && message.text.includes('500 (Internal Server Error)')))
    const unexpectedHttpFailures = diagnostics.httpFailures.filter(item => !(expectedSynthetic500 && item.status === 500 && item.path === '/api/setup/progress'))
    console.log(`[browser diagnostics] ${testInfo.title}: console=${diagnostics.consoleErrors.length}, page=${diagnostics.pageErrors.length}, request=${diagnostics.failedRequests.length}, http=${diagnostics.httpFailures.length}, unexpected-api=${diagnostics.apiFailures.length - expectedApiFailures}, expected-api=${expectedApiFailures}`)
    for (const failure of diagnostics.httpFailures) console.log(`[browser HTTP] ${testInfo.title}: ${failure.status} ${failure.method} ${failure.path}`)
    for (const message of diagnostics.consoleErrors) {
      const safeMessage = message.text
        .replace(/https?:\/\/[^\s)]+/g, value => { try { const url = new URL(value); return `${url.origin}${url.pathname}` } catch { return '<url>' } })
        .replace(/[\w.+-]+@[\w.-]+\.[A-Z]{2,}/gi, '<email>')
        .slice(0, 300)
      console.log(`[browser console] ${testInfo.title}: ${safeMessage} [source=${message.source}]`)
    }
    await testInfo.attach('browser-diagnostics', { body: JSON.stringify(diagnostics, null, 2), contentType: 'application/json' })
    expect(unexpectedConsoleErrors, 'unexpected browser console errors').toEqual([])
    expect(diagnostics.pageErrors, 'uncaught browser errors').toEqual([])
    expect(diagnostics.failedRequests, 'failed browser requests').toEqual([])
    expect(unexpectedHttpFailures, 'unexpected HTTP failures').toEqual([])
    expect(diagnostics.apiFailures.filter(item => !item.expected), 'unexpected API failures').toEqual([])
  },
})

export const newAccount = (label = 'e2e'): Account => ({
  name: `MoneyBoard ${label}`,
  email: `${label}-${randomUUID()}@example.test`,
  password: 'SyntheticTest123!',
})

export async function register(page: Page, account = newAccount()) {
  await page.goto('/')
  await page.getByRole('button', { name: /New to MoneyBoard\? Create an account/i }).click()
  await page.getByLabel('Your name').fill(account.name)
  await page.getByLabel('Email').fill(account.email)
  await page.getByLabel('Password').fill(account.password)
  await page.getByRole('button', { name: /Create account/i }).click()
  await expect(page.getByRole('heading', { name: 'Welcome to MoneyBoard' })).toBeVisible()
  return account
}

export async function completeSetup(page: Page, goalName = 'E2E savings goal') {
  await page.getByRole('button', { name: /Start my financial setup/i }).click()
  await expect(page.getByText('STEP 1 OF 5')).toBeVisible()
  await page.getByRole('button', { name: /Continue/i }).click()
  await page.getByRole('textbox', { name: 'Income source' }).fill('Synthetic salary')
  await page.getByRole('spinbutton', { name: 'Amount' }).fill('4200')
  await page.getByRole('button', { name: /Continue/i }).click()
  const planned = page.getByRole('spinbutton', { name: 'Monthly plan' })
  await planned.nth(0).fill('1100')
  await planned.nth(1).fill('250')
  await planned.nth(2).fill('450')
  await page.getByRole('button', { name: /Continue/i }).click()
  await expect(page.getByText('STEP 4 OF 5')).toBeVisible()
  await page.getByRole('button', { name: /Add savings goal/i }).click()
  await page.getByRole('textbox', { name: 'Goal name' }).fill(goalName)
  await page.getByRole('spinbutton', { name: 'Target amount' }).fill('3000')
  await page.getByRole('spinbutton', { name: 'Monthly contribution plan' }).fill('150')
  await page.getByRole('button', { name: /Continue/i }).click()
  await expect(page.getByRole('heading', { name: 'Your starting plan' })).toBeVisible()
  await page.getByRole('button', { name: /Create my MoneyBoard/i }).click()
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible()
}

export async function logout(page: Page) {
  await page.locator('button.profile').click()
  await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible()
}

export { expect }
