import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import Onboarding from './Onboarding'
import type { SetupDraft, User } from './api'

const user: User = { id: 'u1', email: 'user@example.test', displayName: 'User', currency: 'GBP', setupCompleted: false, setupProgress: null, setupData: null, hasFinancialRecords: false }
const draft: SetupDraft = { currency: 'GBP', incomes: [{ name: 'Salary', amount: '4000', frequency: 'Monthly' }], expenses: [{ name: 'Housing', amount: '1000' }, { name: 'Utilities', amount: '250' }, { name: 'Food', amount: '500' }], goals: [] }
const ok = (body: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response
const advanceToReview = async (userEventInstance: ReturnType<typeof userEvent.setup>) => {
  await userEventInstance.click(screen.getByRole('button', { name: /Start my financial setup/i }))
  await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
  await userEventInstance.type(screen.getByLabelText('Amount'), '4000')
  await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
  for (const [field, amount] of [['Monthly plan', '1000'], ['Monthly plan', '250'], ['Monthly plan', '500']]) {
    const inputs = screen.getAllByLabelText(field)
    const target = inputs.find(input => !(input as HTMLInputElement).value)
    await userEventInstance.type(target!, amount)
  }
  await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
  await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
}

describe('financial setup wizard', () => {
  it('navigates all five steps, validates inputs, and keeps entries when moving back', async () => {
    const userEventInstance = userEvent.setup()
    vi.stubGlobal('fetch', vi.fn(async () => ok({ saved: true })))
    render(<Onboarding token="test-token" user={user} onComplete={async () => {}} onSkip={() => {}} onCancel={() => {}} />)

    expect(screen.getByRole('heading', { name: 'Welcome to MoneyBoard' })).toBeTruthy()
    await userEventInstance.click(screen.getByRole('button', { name: /Start my financial setup/i }))
    expect(screen.getByText('STEP 1 OF 5')).toBeTruthy()
    await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
    expect(screen.getByText('STEP 2 OF 5')).toBeTruthy()
    await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
    expect(screen.getByRole('alert').textContent).toContain('Enter a positive amount')
    await userEventInstance.type(screen.getByLabelText('Amount'), '4000')
    await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
    expect(screen.getByText('STEP 3 OF 5')).toBeTruthy()
    await userEventInstance.click(screen.getByRole('button', { name: /Back/i }))
    expect((screen.getByLabelText('Amount') as HTMLInputElement).value).toBe('4000')
    await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
    await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
    expect(screen.getByRole('alert').textContent).toContain('positive planned amount')
    for (const amount of ['1000', '250', '500']) {
      const target = screen.getAllByLabelText('Monthly plan').find(input => !(input as HTMLInputElement).value)!
      await userEventInstance.type(target, amount)
    }
    await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
    expect(screen.getByText('STEP 4 OF 5')).toBeTruthy()
    await userEventInstance.click(screen.getByRole('button', { name: /Add savings goal/i }))
    await userEventInstance.type(screen.getByLabelText('Goal name'), 'Emergency fund')
    await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
    expect(screen.getByRole('alert').textContent).toContain('positive target amount')
    await userEventInstance.type(screen.getByLabelText('Target amount'), '1000')
    await userEventInstance.click(screen.getByRole('button', { name: /Continue/i }))
    expect(screen.getByText('STEP 5 OF 5')).toBeTruthy()
    expect(screen.getByText('£2,250.00')).toBeTruthy()
  })

  it('resumes the saved step and skips without submitting completion', async () => {
    const userEventInstance = userEvent.setup()
    const onSkip = vi.fn()
    const fetchMock = vi.fn(async () => ok({ saved: true }))
    vi.stubGlobal('fetch', fetchMock)
    const returningUser = { ...user, setupProgress: JSON.stringify({ step: 3, data: draft }) }
    render(<Onboarding token="test-token" user={returningUser} onComplete={async () => {}} onSkip={onSkip} onCancel={() => {}} />)

    await userEventInstance.click(screen.getByRole('button', { name: /Continue where I left off/i }))
    expect(screen.getByText('STEP 3 OF 5')).toBeTruthy()
    expect((screen.getAllByLabelText('Monthly plan')[0] as HTMLInputElement).value).toBe('1000')
    await userEventInstance.click(screen.getByRole('button', { name: /Skip for now/i }))
    expect(onSkip).toHaveBeenCalledOnce()
    expect(fetchMock).not.toHaveBeenCalledWith(expect.stringContaining('/setup/complete'), expect.anything())
  })

  it('recovers from an older or incomplete saved draft instead of crashing when setup is opened', async () => {
    const userEventInstance = userEvent.setup()
    const profileWithIncompleteDraft = { ...user, setupProgress: JSON.stringify({ step: 3, data: {} }) }
    render(<Onboarding token="test-token" user={profileWithIncompleteDraft} onComplete={async () => {}} onSkip={() => {}} onCancel={() => {}} />)

    expect(screen.getByRole('heading', { name: 'Welcome to MoneyBoard' })).toBeTruthy()
    await userEventInstance.click(screen.getByRole('button', { name: /Continue where I left off/i }))
    expect(screen.getByText('STEP 3 OF 5')).toBeTruthy()
    expect(screen.getAllByLabelText('Monthly plan')).toHaveLength(3)
  })

  it('posts the reviewed plan and navigates only after successful API confirmation', async () => {
    const userEventInstance = userEvent.setup()
    const onComplete = vi.fn(async () => {})
    let posted: Record<string, unknown> | undefined
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith('/setup/complete')) posted = JSON.parse(String(init?.body)) as Record<string, unknown>
      return ok({ completed: true })
    }))
    render(<Onboarding token="test-token" user={user} onComplete={onComplete} onSkip={() => {}} onCancel={() => {}} />)
    await advanceToReview(userEventInstance)
    await userEventInstance.click(screen.getByRole('button', { name: /Create my MoneyBoard/i }))

    await waitFor(() => expect(onComplete).toHaveBeenCalledOnce())
    expect((posted?.incomes as Array<{ amount: number }>)[0].amount).toBe(4000)
    expect((posted?.expenses as Array<{ amount: number }>).map(item => item.amount)).toEqual([1000, 250, 500])
    expect(posted?.goals).toEqual([])
  })

  it('shows API failures and does not report completion', async () => {
    const userEventInstance = userEvent.setup()
    const onComplete = vi.fn(async () => {})
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => String(input).endsWith('/setup/complete') ? ok({ error: 'Save failed' }, 500) : ok({ saved: true })))
    render(<Onboarding token="test-token" user={user} onComplete={onComplete} onSkip={() => {}} onCancel={() => {}} />)
    await advanceToReview(userEventInstance)
    await userEventInstance.click(screen.getByRole('button', { name: /Create my MoneyBoard/i }))

    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.getByRole('alert').textContent).toContain('Save failed')
    expect(onComplete).not.toHaveBeenCalled()
  })

  it('shows a useful error when saving incomplete setup progress fails', async () => {
    const userEventInstance = userEvent.setup()
    vi.stubGlobal('fetch', vi.fn(async () => ok({ error: 'Progress unavailable' }, 503)))
    render(<Onboarding token="test-token" user={user} onComplete={async () => {}} onSkip={() => {}} onCancel={() => {}} />)

    await userEventInstance.click(screen.getByRole('button', { name: /Start my financial setup/i }))
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.getByRole('alert').textContent).toContain('Progress unavailable')
  })

  it('keeps the save button busy until completion is confirmed', async () => {
    const userEventInstance = userEvent.setup()
    let finish!: (value: Response) => void
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => String(input).endsWith('/setup/complete') ? await new Promise<Response>(resolve => { finish = resolve }) : ok({ saved: true })))
    const onComplete = vi.fn(async () => {})
    render(<Onboarding token="test-token" user={user} onComplete={onComplete} onSkip={() => {}} onCancel={() => {}} />)
    await advanceToReview(userEventInstance)
    await userEventInstance.click(screen.getByRole('button', { name: /Create my MoneyBoard/i }))
    const saving = await screen.findByRole('button', { name: /Saving your setup/i })
    expect((saving as HTMLButtonElement).disabled).toBe(true)
    finish(ok({ completed: true }))
    await waitFor(() => expect(onComplete).toHaveBeenCalledOnce())
  })
})
