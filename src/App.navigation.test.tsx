import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
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

  it('shows setup income separately from actual income transactions', async () => {
    const setupProfile = {
      ...profile,
      setupCompleted: true,
      setupProgress: null,
      setupData: JSON.stringify({ Incomes: [
        { Name: 'Weekly work', Amount: 120, Frequency: 'Weekly' },
        { Name: 'Fortnightly work', Amount: 300, Frequency: 'Fortnightly' },
        { Name: 'Monthly work', Amount: 1500, Frequency: 'Monthly' },
        { Name: 'Annual work', Amount: 2160, Frequency: 'Annually' },
      ] }),
    }
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/auth/me')) return ok(setupProfile)
      if (url.includes('/dashboard?')) return ok({ year: 2026, month: 10, balance: 0, income: 0, expenses: 0, saved: 0, savingsTarget: 0, budgets: [], trend: [], recent: [] })
      return ok([])
    }))
    render(<App />)

    const expected = await screen.findByText('Expected Monthly Income')
    expect(within(expected.closest('.summary-card') as HTMLElement).getByText('£2,850.00')).toBeTruthy()
    const actual = screen.getByText('Money in').closest('.summary-card') as HTMLElement
    expect(within(actual).getByText('£0.00')).toBeTruthy()
  })

  it('clarifies that the overview shows an all-time transaction net and current goal amounts', async () => {
    render(<App />)

    expect(await screen.findByText('All-time net transactions')).toBeTruthy()
    expect(screen.getByText('Saved toward goals')).toBeTruthy()
    expect(screen.getByText('Current amounts across 0 goals; not this month’s contributions')).toBeTruthy()
  })

  it('initializes the dashboard month from the local calendar date', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-09-30T23:30:00.000Z'))
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/auth/me')) return ok(profile)
      if (url.includes('/dashboard?')) return ok({ year: 2026, month: 10, balance: 0, income: 0, expenses: 0, saved: 0, savingsTarget: 0, budgets: [], trend: [], recent: [] })
      return ok([])
    })
    vi.stubGlobal('fetch', fetchMock)
    const localNow = new Date()
    const expectedPeriod = `${localNow.getFullYear()}-${String(localNow.getMonth() + 1).padStart(2, '0')}`
    render(<App />)

    await screen.findByRole('button', { name: 'Financial setup' })
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes(`/dashboard?year=${expectedPeriod.slice(0, 4)}&month=${Number(expectedPeriod.slice(5, 7))}`))).toBe(true)
  })

  it('shows a safe placeholder when setup income data is missing', async () => {
    render(<App />)

    const expected = await screen.findByText('Expected Monthly Income')
    expect(within(expected.closest('.summary-card') as HTMLElement).getByText('Not set')).toBeTruthy()
  })

  it('keeps an existing custom expense category selected and available when editing', async () => {
    const existing = { id: 'expense-1', type: 'expense' as const, title: 'Garden supplies', amount: 18, date: '2026-10-12', category: 'Garden', notes: null, createdAt: '2026-10-12T12:00:00Z' }
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/auth/me')) return ok(profile)
      if (url.includes('/dashboard?')) return ok({ year: 2026, month: 10, balance: -18, income: 0, expenses: 18, saved: 0, savingsTarget: 0, budgets: [], trend: [], recent: [existing] })
      if (url.endsWith('/transactions')) return ok([existing])
      return ok([])
    }))
    const user = userEvent.setup()
    render(<App />)

    await user.click(await screen.findByRole('button', { name: 'Transactions' }))
    const row = (await screen.findByText('Garden supplies')).closest('.transaction-row') as HTMLElement
    await user.click(within(row).getByRole('button', { name: 'Edit' }))

    const category = screen.getByLabelText('Category') as HTMLSelectElement
    expect(category.value).toBe('Garden')
    expect(within(category).getByRole('option', { name: 'Garden' })).toBeTruthy()
  })

  it('refreshes budget figures after expense create, edit and delete while keeping unbudgeted expenses in Money out', async () => {
    const entries: Array<{ id: string; type: 'income' | 'expense'; title: string; amount: number; date: string; category: string; notes: string | null; createdAt: string }> = [
      { id: 'garden-1', type: 'expense', title: 'Garden supplies', amount: 8, date: '2026-10-10', category: 'Garden', notes: null, createdAt: '2026-10-10T12:00:00Z' },
    ]
    const getPeriodExpenses = () => entries.filter(entry => entry.type === 'expense' && entry.date.startsWith('2026-10'))
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/auth/me')) return ok(profile)
      if (url.includes('/dashboard?')) {
        const expenses = getPeriodExpenses().reduce((total, entry) => total + entry.amount, 0)
        return ok({ year: 2026, month: 10, balance: -expenses, income: 0, expenses, saved: 0, savingsTarget: 0, budgets: [], trend: [{ month: 'Oct', income: 0, expenses }], recent: [...entries].reverse() })
      }
      if (url.endsWith('/transactions') && init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as Omit<typeof entries[number], 'id' | 'createdAt'>
        const entry = { ...body, id: `expense-${entries.length + 1}`, createdAt: '2026-10-24T12:00:00Z' }
        entries.push(entry)
        return ok(entry)
      }
      if (/\/transactions\/expense\/[^/]+$/.test(url) && init?.method === 'PUT') {
        const id = url.split('/').pop()
        const index = entries.findIndex(entry => entry.id === id)
        const body = JSON.parse(String(init.body)) as Omit<typeof entries[number], 'id' | 'createdAt'>
        entries[index] = { ...entries[index], ...body }
        return ok(entries[index])
      }
      if (/\/transactions\/expense\/[^/]+$/.test(url) && init?.method === 'DELETE') {
        const id = url.split('/').pop()
        entries.splice(entries.findIndex(entry => entry.id === id), 1)
        return { ok: true, status: 204, json: async () => undefined } as Response
      }
      if (url.endsWith('/transactions')) return ok(entries)
      if (url.includes('/budgets?')) {
        const actual = getPeriodExpenses().filter(entry => entry.category.toLowerCase() === 'food').reduce((total, entry) => total + entry.amount, 0)
        return ok([{ id: 'food-budget', name: 'Food', limit: 100, actual, remaining: 100 - actual, percentUsed: actual, year: 2026, month: 10 }])
      }
      return ok([])
    })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('confirm', vi.fn(() => true))
    const user = userEvent.setup()
    render(<App />)

    await screen.findByRole('button', { name: 'Financial setup' })
    const moneyOutAmount = () => within(screen.getByText('Money out').closest('.summary-card') as HTMLElement).getByText(/£\d+\.\d{2}/)
    const budgetCard = () => screen.getByText('Spending plan').closest('.budget-panel') as HTMLElement
    const budgetTotalText = () => budgetCard().querySelector('.budget-total')?.textContent ?? ''
    const budgetDetail = () => screen.getByText('Food').closest('.budget-detail') as HTMLElement
    expect(moneyOutAmount().textContent).toBe('£8.00')
    expect(budgetTotalText()).toContain('£0.00')
    expect(budgetTotalText()).toContain('£100.00')

    await user.click(screen.getByRole('button', { name: 'Transactions' }))
    await user.click(screen.getAllByRole('button', { name: 'Add transaction' }).slice(-1)[0])
    await user.type(screen.getByLabelText('What was it for?'), 'Groceries')
    await user.type(screen.getByLabelText('Amount'), '20')
    fireEvent.change(screen.getByLabelText('Date'), { target: { value: '2026-10-24' } })
    await user.click(screen.getByRole('button', { name: /Save transaction/i }))
    await user.click(screen.getByRole('button', { name: 'Overview' }))
    await waitFor(() => expect(moneyOutAmount().textContent).toBe('£28.00'))
    expect(budgetTotalText()).toContain('£20.00')
    await user.click(screen.getByRole('button', { name: 'Budgets' }))
    expect(within(budgetDetail()).getByText('£80.00')).toBeTruthy()

    await user.click(screen.getByRole('button', { name: 'Transactions' }))
    const groceriesRow = screen.getByText('Groceries').closest('.transaction-row') as HTMLElement
    await user.click(within(groceriesRow).getByRole('button', { name: 'Edit' }))
    const amount = screen.getByLabelText('Amount')
    await user.clear(amount)
    await user.type(amount, '25')
    await user.click(screen.getByRole('button', { name: /Save changes/i }))
    await user.click(screen.getByRole('button', { name: 'Overview' }))
    await waitFor(() => expect(moneyOutAmount().textContent).toBe('£33.00'))
    expect(budgetTotalText()).toContain('£25.00')

    await user.click(screen.getByRole('button', { name: 'Transactions' }))
    const updatedGroceriesRow = screen.getByText('Groceries').closest('.transaction-row') as HTMLElement
    await user.click(within(updatedGroceriesRow).getByRole('button', { name: 'Delete' }))
    await user.click(screen.getByRole('button', { name: 'Overview' }))
    await waitFor(() => expect(moneyOutAmount().textContent).toBe('£8.00'))
    expect(moneyOutAmount().textContent).toBe('£8.00')
    expect(budgetTotalText()).toContain('£0.00')
    expect(entries).toHaveLength(1)
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
