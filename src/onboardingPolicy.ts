import type { User } from './api'

export function shouldOfferOnboarding(user: User, skipped: boolean): boolean {
  return !user.setupCompleted && !user.hasFinancialRecords && !skipped
}
