export type TransactionType = 'income' | 'expense'

export type Transaction = {
  id: string
  type: TransactionType
  title: string
  amount: number
  date: string
  category: string
  notes: string | null
  createdAt: string
}

export type Budget = {
  id: string
  name: string
  limit: number
  actual: number
  remaining: number
  percentUsed: number
  year: number
  month: number
}

export type SavingsGoal = {
  id: string
  name: string
  targetAmount: number
  currentAmount: number
  targetDate: string | null
  monthlyContribution: number
  percentComplete: number
}

export type DebtPayment = { id: string; debtId: string; amount: number; date: string; notes: string | null }
export type Debt = {
  id: string
  name: string
  originalAmount: number
  balance: number
  interestRate: number
  minimumPayment: number
  frequency: string
  nextDueDate: string | null
  status: string
  payments: DebtPayment[]
}

export type Dashboard = {
  year: number
  month: number
  balance: number
  income: number
  expenses: number
  saved: number
  savingsTarget: number
  budgets: Budget[]
  trend: { month: string; income: number; expenses: number }[]
  recent: Transaction[]
}

export type User = { id: string; email: string; displayName: string; currency: string; setupCompleted: boolean; setupProgress: string | null; setupData: string | null; hasFinancialRecords: boolean }
export type SetupDraft = { currency: string; incomes: { name: string; amount: string; frequency: string }[]; expenses: { name: string; amount: string }[]; goals: { name: string; targetAmount: string; monthlyContribution: string; targetDate: string }[] }
export type LoginResult = { accessToken: string; expiresAt: string; user: User }

const apiOrigin = 'http://localhost:5080/api'

export class ApiRequestError extends Error {
  constructor(message: string, readonly status?: number) { super(message); this.name = 'ApiRequestError' }
}

export async function request<T>(path: string, token?: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)
  let response: Response
  try {
    response = await fetch(`${apiOrigin}${path}`, { ...init, headers })
  } catch {
    throw new ApiRequestError('Unable to connect. Please try again.')
  }
  if (!response.ok) {
    if (response.status === 401) throw new ApiRequestError('Authentication failed.', 401)
    const body = await response.json().catch(() => null) as { error?: string; detail?: string; title?: string; errors?: Record<string, string[]> } | null
    const fieldError = body?.errors ? Object.values(body.errors).flat()[0] : undefined
    throw new ApiRequestError(fieldError || body?.error || body?.title || 'Something went wrong. Please try again.', response.status)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const moneyApi = {
  login: (email: string, password: string) => request<LoginResult>('/auth/login', undefined, { method: 'POST', body: JSON.stringify({ email, password }) }),
  register: (name: string, email: string, password: string) => request<unknown>('/auth/register', undefined, { method: 'POST', body: JSON.stringify({ name, email, password }) }),
  forgotPassword: (email: string) => request<{ message: string }>('/auth/forgot-password', undefined, { method: 'POST', body: JSON.stringify({ email }) }),
  resetPassword: (email: string, token: string, newPassword: string) => request<{ message: string }>('/auth/reset-password', undefined, { method: 'POST', body: JSON.stringify({ email, token, newPassword }) }),
  me: (token: string) => request<User>('/auth/me', token),
  saveSetupProgress: (token: string, step: number, data: SetupDraft) => request<{ saved: boolean }>('/setup/progress', token, { method: 'PUT', body: JSON.stringify({ step, data }) }),
  completeSetup: (token: string, data: SetupDraft, period: string) => request<{ completed: boolean }>('/setup/complete', token, { method: 'POST', body: JSON.stringify({ currency: data.currency, incomes: data.incomes.filter(x => x.name.trim() && Number(x.amount) > 0).map(x => ({ name: x.name.trim(), amount: Number(x.amount), frequency: x.frequency })), expenses: data.expenses.filter(x => x.name.trim() && Number(x.amount) > 0).map(x => ({ name: x.name.trim(), amount: Number(x.amount) })), goals: data.goals.filter(x => x.name.trim() && Number(x.targetAmount) > 0).map(x => ({ name: x.name.trim(), targetAmount: Number(x.targetAmount), monthlyContribution: Number(x.monthlyContribution) || 0, targetDate: x.targetDate || null })), year: Number(period.slice(0, 4)), month: Number(period.slice(5, 7)) }) }),
  dashboard: (token: string, period: string) => request<Dashboard>(`/dashboard?year=${period.slice(0, 4)}&month=${Number(period.slice(5, 7))}`, token),
  transactions: (token: string) => request<Transaction[]>('/transactions', token),
  saveTransaction: (token: string, data: Omit<Transaction, 'id' | 'createdAt'>, existing?: Transaction) => request<Transaction>(existing ? `/transactions/${existing.type}/${existing.id}` : '/transactions', token, { method: existing ? 'PUT' : 'POST', body: JSON.stringify({ ...data, type: existing?.type ?? data.type }) }),
  deleteTransaction: (token: string, tx: Transaction) => request<void>(`/transactions/${tx.type}/${tx.id}`, token, { method: 'DELETE' }),
  budgets: (token: string, period: string) => request<Budget[]>(`/budgets?year=${period.slice(0, 4)}&month=${Number(period.slice(5, 7))}`, token),
  saveBudget: (token: string, data: Pick<Budget, 'name' | 'limit' | 'year' | 'month'>, id?: string) => request<Budget>(id ? `/budgets/${id}` : '/budgets', token, { method: id ? 'PUT' : 'POST', body: JSON.stringify(data) }),
  deleteBudget: (token: string, id: string) => request<void>(`/budgets/${id}`, token, { method: 'DELETE' }),
  goals: (token: string) => request<SavingsGoal[]>('/savings-goals', token),
  saveGoal: (token: string, data: Omit<SavingsGoal, 'id' | 'percentComplete'>, id?: string) => request<SavingsGoal>(id ? `/savings-goals/${id}` : '/savings-goals', token, { method: id ? 'PUT' : 'POST', body: JSON.stringify(data) }),
  deleteGoal: (token: string, id: string) => request<void>(`/savings-goals/${id}`, token, { method: 'DELETE' }),
  debts: (token: string) => request<Debt[]>('/debts', token),
  saveDebt: (token: string, data: Omit<Debt, 'id' | 'payments'>, id?: string) => request<Debt>(id ? `/debts/${id}` : '/debts', token, { method: id ? 'PUT' : 'POST', body: JSON.stringify(data) }),
  deleteDebt: (token: string, id: string) => request<void>(`/debts/${id}`, token, { method: 'DELETE' }),
  savePayment: (token: string, debtId: string, data: Pick<DebtPayment, 'amount' | 'date' | 'notes'>, id?: string) => request<DebtPayment>(id ? `/debts/${debtId}/payments/${id}` : `/debts/${debtId}/payments`, token, { method: id ? 'PUT' : 'POST', body: JSON.stringify(data) }),
  deletePayment: (token: string, debtId: string, id: string) => request<void>(`/debts/${debtId}/payments/${id}`, token, { method: 'DELETE' }),
}
