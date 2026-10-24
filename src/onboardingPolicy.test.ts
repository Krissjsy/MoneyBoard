import { describe, expect, it } from 'vitest'
import type { User } from './api'
import { shouldOfferOnboarding } from './onboardingPolicy'

const baseUser: User = { id: 'u1', email: 'user@example.test', displayName: 'User', currency: 'GBP', setupCompleted: false, setupProgress: null, setupData: null, hasFinancialRecords: false }

describe('onboarding entry policy', () => {
  it('offers setup to a genuinely new account', () => {
    expect(shouldOfferOnboarding(baseUser, false)).toBe(true)
  })

  it('does not force onboarding for an account with financial records', () => {
    expect(shouldOfferOnboarding({ ...baseUser, hasFinancialRecords: true }, false)).toBe(false)
  })

  it('does not force onboarding after a completed setup or a local skip', () => {
    expect(shouldOfferOnboarding({ ...baseUser, setupCompleted: true }, false)).toBe(false)
    expect(shouldOfferOnboarding(baseUser, true)).toBe(false)
  })
})
