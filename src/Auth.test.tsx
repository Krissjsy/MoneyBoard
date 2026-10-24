import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const response = (body: unknown, status: number) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response

describe('registration feedback', () => {
  beforeEach(() => localStorage.removeItem('moneyboard.accessToken'))

  it('shows the API duplicate-email conflict and does not attempt login', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => String(input).endsWith('/auth/register')
      ? response({ error: 'An account with this email address is already registered.' }, 409)
      : response({}, 200))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.click(screen.getByRole('button', { name: /New to MoneyBoard\? Create an account/i }))
    await user.type(screen.getByLabelText('Your name'), 'Existing User')
    await user.type(screen.getByLabelText('Email'), 'existing@example.test')
    await user.type(screen.getByLabelText('Password'), 'Password123!')
    await user.click(screen.getByRole('button', { name: /Create account/i }))

    expect((await screen.findByRole('alert')).textContent).toContain('already registered')
    expect(fetchMock.mock.calls.some(([input]) => String(input).endsWith('/auth/login'))).toBe(false)
  })
})
