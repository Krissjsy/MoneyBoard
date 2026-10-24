import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

vi.mock('recharts', () => ({
  Area: () => null,
  AreaChart: ({ data }: { data: unknown }) => <div data-testid="spending-chart-data">{JSON.stringify(data)}</div>,
  CartesianGrid: () => null,
  ResponsiveContainer: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
}))

const profile = {
  id: 'existing-user', email: 'existing@example.test', displayName: 'Existing User', currency: 'GBP',
  setupCompleted: false, setupProgress: JSON.stringify({ step: 3, data: {} }), setupData: null, hasFinancialRecords: true,
}

const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body }) as Response

describe('financial setup navigation', () => {
  beforeEach(() => {
    localStorage.setItem('moneyboard.accessToken', 'test-token')
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/auth/me')) return ok(profile)
      if (url.includes('/dashboard?')) return ok({ year: 2026, month: 10, balance: 0, income: 0, expenses: 0, saved: 0, savingsTarget: 0, budgets: [], trend: [], recent: [] })
      return ok([])
    }))
  })

  it('opens the setup page from the dashboard for existing users with a partial saved draft', async () => {
    const user = userEvent.setup()
    render(<App />)

    const setupButton = await screen.findByRole('button', { name: 'Financial setup' })
    expect(screen.getByRole('heading', { name: /Good to see you, Existing/i })).toBeTruthy()
    await user.click(setupButton)

    expect(await screen.findByRole('heading', { name: 'Review your financial setup' })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: /Continue where I left off/i }))
    expect(screen.getByText('STEP 3 OF 5')).toBeTruthy()
    expect(screen.getAllByLabelText('Monthly plan')).toHaveLength(3)
  })

  it('refreshes dashboard totals, recent activity and chart data after transaction saves and edits', async () => {
    const entries: Array<{ id: string; type: 'income' | 'expense'; title: string; amount: number; date: string; category: string; notes: string | null; createdAt: string }> = []
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/auth/me')) return ok(profile)
      if (url.includes('/dashboard?')) {
        const income = entries.filter(x => x.type === 'income').reduce((sum, item) => sum + item.amount, 0)
        const expenses = entries.filter(x => x.type === 'expense').reduce((sum, item) => sum + item.amount, 0)
        return ok({ year: 2026, month: 10, balance: income - expenses, income, expenses, saved: 0, savingsTarget: 0, budgets: [], trend: [{ month: 'Oct', income, expenses }], recent: [...entries].reverse() })
      }
      if (url.endsWith('/transactions') && init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as Omit<typeof entries[number], 'id' | 'createdAt'>
        const entry = { ...body, id: `tx-${entries.length + 1}`, createdAt: '2026-10-24T12:00:00Z' }
        entries.push(entry)
        return ok(entry)
      }
      if (/\/transactions\/(income|expense)\/tx-\d+$/.test(url) && init?.method === 'PUT') {
        const segments = url.split('/')
        const id = segments[segments.length - 1]
        const index = entries.findIndex(x => x.id === id)
        const body = JSON.parse(String(init.body)) as Omit<typeof entries[number], 'id' | 'createdAt'>
        entries[index] = { ...entries[index], ...body }
        return ok(entries[index])
      }
      if (url.endsWith('/transactions')) return ok(entries)
      return ok([])
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await screen.findByRole('button', { name: 'Financial setup' })
    await user.click(screen.getByRole('button', { name: 'Add transaction' }))
    await user.click(screen.getByRole('button', { name: 'Money in' }))
    await user.type(screen.getByLabelText('Income source'), 'Pay')
    await user.type(screen.getByLabelText('Amount'), '100')
    await user.click(screen.getByRole('button', { name: /Save transaction/i }))

    await screen.findAllByText('£100.00')
    await user.click(screen.getByRole('button', { name: 'Add transaction' }))
    await user.type(screen.getByLabelText('What was it for?'), 'Groceries')
    await user.type(screen.getByLabelText('Amount'), '25')
    await user.click(screen.getByRole('button', { name: /Save transaction/i }))

    await screen.findByText('£75.00')
    const expenseRow = screen.getByText('Groceries').closest('.transaction-row') as HTMLElement
    await user.click(within(expenseRow).getByRole('button', { name: 'Edit' }))
    const amount = screen.getByLabelText('Amount')
    await user.clear(amount)
    await user.type(amount, '30')
    await user.click(screen.getByRole('button', { name: /Save changes/i }))

    await screen.findByText('£70.00')
    expect(screen.getAllByText('£100.00').length).toBeGreaterThan(0)
    expect(screen.getAllByText('£30.00').length).toBeGreaterThan(0)
    expect(screen.getByText('−£30.00')).toBeTruthy()
    expect(JSON.parse(screen.getByTestId('spending-chart-data').textContent!)).toEqual([{ month: 'Oct', income: 100, expenses: 30 }])
    expect(entries).toHaveLength(2)
  })
})
