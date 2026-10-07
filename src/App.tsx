import { useCallback, useEffect, useMemo, useState } from 'react'
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis } from 'recharts'
import { ArrowDownLeft, ArrowLeftRight, ArrowRight, ArrowUpRight, Bell, CalendarDays, ChevronDown, CircleHelp, CreditCard, Ellipsis, House, Leaf, LogOut, Plus, Search, Sparkles, Target, Wallet, X } from 'lucide-react'
import { moneyApi, type Budget, type Dashboard, type Debt, type DebtPayment, type SavingsGoal, type Transaction, type TransactionType, type User } from './api'

const currency = (amount: number) => new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' }).format(amount)
const today = () => new Date().toISOString().slice(0, 10)
const currentPeriod = () => new Date().toISOString().slice(0, 7)
const prettyDate = (date: string) => new Date(`${date}T00:00:00`).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })
const categories = ['Food', 'Housing', 'Transport', 'Utilities', 'Shopping', 'Entertainment', 'Subscriptions', 'Health', 'Travel', 'Other']
const blankTransaction = () => ({ type: 'expense' as TransactionType, title: '', amount: '', date: today(), category: 'Food', notes: '' })

function App() {
  const [tab, setTab] = useState('Overview')
  const [token, setToken] = useState(() => localStorage.getItem('moneyboard.accessToken') ?? '')
  const [user, setUser] = useState<User | null>(null)
  const [authMode, setAuthMode] = useState<'login' | 'register'>('login')
  const [authForm, setAuthForm] = useState({ name: '', email: '', password: '' })
  const [period, setPeriod] = useState(currentPeriod())
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [transactions, setTransactions] = useState<Transaction[]>([])
  const [budgets, setBudgets] = useState<Budget[]>([])
  const [goals, setGoals] = useState<SavingsGoal[]>([])
  const [debts, setDebts] = useState<Debt[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [transactionModal, setTransactionModal] = useState(false)
  const [editingTransaction, setEditingTransaction] = useState<Transaction | null>(null)
  const [transactionForm, setTransactionForm] = useState(blankTransaction)

  const [budgetFormOpen, setBudgetFormOpen] = useState(false)
  const [editingBudget, setEditingBudget] = useState<string | null>(null)
  const [budgetForm, setBudgetForm] = useState({ name: '', limit: '', year: Number(period.slice(0, 4)), month: Number(period.slice(5, 7)) })
  const [goalFormOpen, setGoalFormOpen] = useState(false)
  const [editingGoal, setEditingGoal] = useState<string | null>(null)
  const [goalForm, setGoalForm] = useState({ name: '', targetAmount: '', currentAmount: '0', targetDate: '', monthlyContribution: '0' })
  const [debtFormOpen, setDebtFormOpen] = useState(false)
  const [editingDebt, setEditingDebt] = useState<string | null>(null)
  const [debtForm, setDebtForm] = useState({ name: '', originalAmount: '', balance: '', interestRate: '0', minimumPayment: '0', frequency: 'Monthly', nextDueDate: '', status: 'Active' })
  const [paymentTarget, setPaymentTarget] = useState<string | null>(null)
  const [editingPayment, setEditingPayment] = useState<string | null>(null)
  const [paymentForm, setPaymentForm] = useState({ amount: '', date: today(), notes: '' })

  const monthLabel = useMemo(() => {
    const [year, month] = period.split('-').map(Number)
    return new Date(year, month - 1, 1).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' })
  }, [period])

  const loadData = useCallback(async () => {
    const [summary, items, budgetRows, goalRows, debtRows] = await Promise.all([
      moneyApi.dashboard(token, period), moneyApi.transactions(token), moneyApi.budgets(token, period), moneyApi.goals(token), moneyApi.debts(token),
    ])
    setDashboard(summary); setTransactions(items); setBudgets(budgetRows); setGoals(goalRows); setDebts(debtRows)
  }, [token, period])

  useEffect(() => {
    if (!token) { setLoading(false); return }
    let active = true
    setLoading(true); setError('')
    Promise.all([moneyApi.me(token), moneyApi.dashboard(token, period), moneyApi.transactions(token), moneyApi.budgets(token, period), moneyApi.goals(token), moneyApi.debts(token)])
      .then(([profile, summary, items, budgetRows, goalRows, debtRows]) => {
        if (!active) return
        setUser(profile); setDashboard(summary); setTransactions(items); setBudgets(budgetRows); setGoals(goalRows); setDebts(debtRows)
      })
      .catch(reason => {
        if (!active) return
        const message = reason instanceof Error ? reason.message : 'Could not load your MoneyBoard data.'
        if (message.includes('(401)')) { localStorage.removeItem('moneyboard.accessToken'); setToken(''); setUser(null) }
        else setError(message)
      })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [token, period])

  function announce(message: string) { setNotice(message); window.setTimeout(() => setNotice(''), 2600) }

  async function refresh() {
    try { await loadData(); setError('') }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not refresh your data.') }
  }

  async function runAction(action: () => Promise<unknown>, message: string): Promise<boolean> {
    setSaving(true); setError('')
    try { await action(); await refresh(); announce(message); return true }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'The change could not be saved.'); return false }
    finally { setSaving(false) }
  }

  async function submitAuth(event: React.FormEvent) {
    event.preventDefault(); setSaving(true); setError('')
    try {
      if (authMode === 'register') await moneyApi.register(authForm.name.trim(), authForm.email.trim(), authForm.password)
      const result = await moneyApi.login(authForm.email.trim(), authForm.password)
      localStorage.setItem('moneyboard.accessToken', result.accessToken); setUser(result.user); setToken(result.accessToken)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not sign in.') }
    finally { setSaving(false) }
  }

  function signOut() {
    localStorage.removeItem('moneyboard.accessToken'); setToken(''); setUser(null); setDashboard(null); setTransactions([]); setBudgets([]); setGoals([]); setDebts([])
  }

  function openTransaction(tx?: Transaction) {
    setEditingTransaction(tx ?? null)
    setTransactionForm(tx ? { type: tx.type, title: tx.title, amount: String(tx.amount), date: tx.date, category: tx.category === 'Income' ? 'Other' : tx.category, notes: tx.notes ?? '' } : blankTransaction())
    setTransactionModal(true)
  }

  async function submitTransaction(event: React.FormEvent) {
    event.preventDefault()
    const data = { type: transactionForm.type, title: transactionForm.title.trim(), amount: Number(transactionForm.amount), date: transactionForm.date, category: transactionForm.category, notes: transactionForm.notes.trim() || null }
    if (await runAction(() => moneyApi.saveTransaction(token, data, editingTransaction ?? undefined), editingTransaction ? 'Transaction updated' : 'Transaction saved')) setTransactionModal(false)
  }

  function editBudget(budget: Budget) {
    setEditingBudget(budget.id); setBudgetForm({ name: budget.name, limit: String(budget.limit), year: budget.year, month: budget.month }); setBudgetFormOpen(true)
  }
  async function submitBudget(event: React.FormEvent) {
    event.preventDefault()
    if (!await runAction(() => moneyApi.saveBudget(token, { name: budgetForm.name.trim(), limit: Number(budgetForm.limit), year: budgetForm.year, month: budgetForm.month }, editingBudget ?? undefined), editingBudget ? 'Budget updated' : 'Budget created')) return
    setBudgetFormOpen(false); setEditingBudget(null); setBudgetForm({ name: '', limit: '', year: Number(period.slice(0, 4)), month: Number(period.slice(5, 7)) })
  }

  function editGoal(goal: SavingsGoal) {
    setEditingGoal(goal.id); setGoalForm({ name: goal.name, targetAmount: String(goal.targetAmount), currentAmount: String(goal.currentAmount), targetDate: goal.targetDate ?? '', monthlyContribution: String(goal.monthlyContribution) }); setGoalFormOpen(true)
  }
  async function submitGoal(event: React.FormEvent) {
    event.preventDefault()
    if (!await runAction(() => moneyApi.saveGoal(token, { name: goalForm.name.trim(), targetAmount: Number(goalForm.targetAmount), currentAmount: Number(goalForm.currentAmount), targetDate: goalForm.targetDate || null, monthlyContribution: Number(goalForm.monthlyContribution) }, editingGoal ?? undefined), editingGoal ? 'Savings goal updated' : 'Savings goal created')) return
    setGoalFormOpen(false); setEditingGoal(null); setGoalForm({ name: '', targetAmount: '', currentAmount: '0', targetDate: '', monthlyContribution: '0' })
  }

  function editDebt(debt: Debt) {
    setEditingDebt(debt.id); setDebtForm({ name: debt.name, originalAmount: String(debt.originalAmount), balance: String(debt.balance), interestRate: String(debt.interestRate), minimumPayment: String(debt.minimumPayment), frequency: debt.frequency, nextDueDate: debt.nextDueDate ?? '', status: debt.status }); setDebtFormOpen(true)
  }
  async function submitDebt(event: React.FormEvent) {
    event.preventDefault()
    if (!await runAction(() => moneyApi.saveDebt(token, { name: debtForm.name.trim(), originalAmount: Number(debtForm.originalAmount), balance: Number(debtForm.balance), interestRate: Number(debtForm.interestRate), minimumPayment: Number(debtForm.minimumPayment), frequency: debtForm.frequency.trim(), nextDueDate: debtForm.nextDueDate || null, status: debtForm.status.trim() }, editingDebt ?? undefined), editingDebt ? 'Debt updated' : 'Debt added')) return
    setDebtFormOpen(false); setEditingDebt(null); setDebtForm({ name: '', originalAmount: '', balance: '', interestRate: '0', minimumPayment: '0', frequency: 'Monthly', nextDueDate: '', status: 'Active' })
  }

  function openPayment(debtId: string, payment?: DebtPayment) {
    setPaymentTarget(debtId); setEditingPayment(payment?.id ?? null); setPaymentForm(payment ? { amount: String(payment.amount), date: payment.date, notes: payment.notes ?? '' } : { amount: '', date: today(), notes: '' })
  }
  async function submitPayment(event: React.FormEvent) {
    event.preventDefault()
    if (!paymentTarget) return
    if (!await runAction(() => moneyApi.savePayment(token, paymentTarget, { amount: Number(paymentForm.amount), date: paymentForm.date, notes: paymentForm.notes.trim() || null }, editingPayment ?? undefined), editingPayment ? 'Payment updated' : 'Payment recorded')) return
    setPaymentTarget(null); setEditingPayment(null); setPaymentForm({ amount: '', date: today(), notes: '' })
  }

  const nav = [{ name: 'Overview', icon: House }, { name: 'Transactions', icon: ArrowLeftRight }, { name: 'Budgets', icon: Wallet }, { name: 'Savings goals', icon: Target }, { name: 'Payments', icon: CreditCard }]

  if (!token) return <div className="auth-shell"><a className="brand" href="#"><span className="brand-icon"><Wallet size={19}/></span><span>moneyboard<span className="brand-period">.</span></span></a><form className="auth-card" onSubmit={submitAuth}><span className="eyebrow"><Sparkles size={14}/> YOUR FINANCIAL SPACE</span><h1>{authMode === 'login' ? 'Welcome back' : 'Create your account'}</h1><p className="subtitle">{authMode === 'login' ? 'Sign in to see your money clearly.' : 'Start with your real income, spending and goals.'}</p>{authMode === 'register' && <label>Your name<input value={authForm.name} onChange={e => setAuthForm({ ...authForm, name: e.target.value })} required maxLength={100}/></label>}<label>Email<input type="email" autoComplete="email" value={authForm.email} onChange={e => setAuthForm({ ...authForm, email: e.target.value })} required/></label><label>Password<input type="password" autoComplete={authMode === 'login' ? 'current-password' : 'new-password'} value={authForm.password} onChange={e => setAuthForm({ ...authForm, password: e.target.value })} minLength={authMode === 'register' ? 10 : undefined} required/></label>{authMode === 'register' && <p className="form-hint">Use at least 10 characters, including a number and symbol.</p>}{error && <div className="error-message" role="alert">{error}</div>}<button className="primary-button auth-submit" disabled={saving}>{saving ? 'Please wait…' : authMode === 'login' ? 'Sign in' : 'Create account'} <ArrowRight size={16}/></button><button className="text-link auth-switch" type="button" onClick={() => { setAuthMode(authMode === 'login' ? 'register' : 'login'); setError('') }}>{authMode === 'login' ? 'New to MoneyBoard? Create an account' : 'Already have an account? Sign in'}</button></form></div>

  const totalBudget = budgets.reduce((sum, budget) => sum + budget.limit, 0)
  const actualBudget = budgets.reduce((sum, budget) => sum + budget.actual, 0)
  const budgetRemaining = totalBudget - actualBudget
  const budgetPercent = totalBudget ? Math.min(100, actualBudget / totalBudget * 100) : 0

  return <div className="app-shell">
    <aside className="sidebar"><a className="brand" href="#"><span className="brand-icon"><Wallet size={19}/></span><span>moneyboard<span className="brand-period">.</span></span></a>
      <div className="space-label">YOUR MONEY</div><nav>{nav.map(({ name, icon: Icon }) => <button key={name} className={`nav-item ${tab === name ? 'active' : ''}`} onClick={() => setTab(name)}><Icon size={18}/><span>{name}</span>{name === 'Payments' && debts.length > 0 && <i className="nav-dot"/>}</button>)}</nav>
      <button className="budget-teaser" onClick={() => setTab('Budgets')}><span className="teaser-heading">{monthLabel} budget <Ellipsis size={17}/></span><strong>{currency(actualBudget)} <small>of {currency(totalBudget)}</small></strong><span className="progress"><i style={{ width: `${budgetPercent}%` }}/></span><span className="teaser-foot">{currency(budgetRemaining)} {budgetRemaining >= 0 ? 'left to spend' : 'over budget'}</span></button>
      <div className="sidebar-bottom"><button className="nav-item" onClick={() => announce('Use the sections to manage your money.') }><CircleHelp size={18}/><span>Help & support</span></button><button className="profile" onClick={signOut}><span className="avatar">{user?.displayName?.slice(0, 1).toUpperCase() ?? 'M'}</span><span className="profile-name"><b>{user?.displayName}</b><small>{user?.email}</small></span><LogOut size={15}/></button></div>
    </aside>
    <main className="main-area"><header className="topbar"><div className="breadcrumb">My space <span>/</span> <b>{tab}</b></div><div className="top-actions"><button className="icon-button search-button" aria-label="Search"><Search size={19}/></button><button className="icon-button bell-button" aria-label="Notifications" onClick={() => announce('You’re all caught up')}><Bell size={19}/></button><span className="top-divider"/><label className="period-picker"><CalendarDays size={14}/><input aria-label="Dashboard month" type="month" value={period} onChange={e => setPeriod(e.target.value)}/></label></div></header>
      <div className="page-content"><div className="greeting-row"><div><span className="eyebrow"><Sparkles size={14}/> YOUR FINANCIAL SNAPSHOT</span><h1>{tab === 'Overview' ? `Good to see you, ${user?.displayName?.split(' ')[0] ?? 'there'}` : tab} <span>✳</span></h1><p className="subtitle">{tab === 'Overview' ? `Here’s how your money is looking in ${monthLabel}.` : `Your ${tab.toLowerCase()} in one place.`}</p></div><button className="primary-button" onClick={() => openTransaction()}><Plus size={18}/> Add transaction</button></div>
        {error && <div className="error-banner" role="alert"><span>{error}</span><button className="text-link" onClick={() => setError('')} aria-label="Dismiss error"><X size={15}/></button></div>}
        {loading ? <div className="loading-state"><span className="spinner"/>Loading your MoneyBoard…</div> : tab === 'Overview' && dashboard ? <>
          <section className="summary-grid"><article className="summary-card balance-card"><div className="card-label">Total balance <span className="info-pill" title="All time income minus expenses">i</span></div><div className="balance-number">{currency(dashboard.balance)}</div><div className="balance-foot"><span className="trend-pill"><ArrowUpRight size={14}/> All time</span><span>income less spending</span></div><div className="balance-mark"><Wallet size={84}/></div></article>
            <article className="summary-card"><div className="card-label">Money in <span className="tiny-icon green"><ArrowDownLeft size={15}/></span></div><div className="summary-number">{currency(dashboard.income)}</div><div className="summary-foot"><span className="summary-note">{monthLabel}</span><span className="mini-bars income-bars"><i/><i/><i/><i/><i/><i/><i/></span></div></article>
            <article className="summary-card"><div className="card-label">Money out <span className="tiny-icon pink"><ArrowUpRight size={15}/></span></div><div className="summary-number">{currency(dashboard.expenses)}</div><div className="summary-foot"><span className="summary-note">{monthLabel}</span><span className="mini-bars expense-bars"><i/><i/><i/><i/><i/><i/><i/></span></div></article>
            <article className="summary-card"><div className="card-label">Set aside <span className="tiny-icon lilac"><Target size={15}/></span></div><div className="summary-number">{currency(dashboard.saved)}</div><div className="summary-foot"><span className="summary-note">Across {goals.length} goals</span><button className="text-link" onClick={() => setTab('Savings goals')}>View goals <ArrowRight size={14}/></button></div></article></section>
          <section className="middle-grid"><article className="panel spending-panel"><div className="panel-heading"><div><h2>Your spending</h2><p>Income and expenses from your transactions.</p></div><span className="select-button">Last 6 months <ChevronDown size={15}/></span></div><div className="chart-legend"><i/> Actual spending <span><b>{currency(dashboard.expenses)}</b> this month</span></div><div className="chart"><ResponsiveContainer width="100%" height="100%"><AreaChart data={dashboard.trend} margin={{ top: 10, right: 8, left: -24, bottom: 0 }}><defs><linearGradient id="spendFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#75b89e" stopOpacity={0.2}/><stop offset="95%" stopColor="#75b89e" stopOpacity={0}/></linearGradient></defs><CartesianGrid vertical={false} stroke="#eef0ed" strokeDasharray="3 4"/><XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fill: '#a1a8a1', fontSize: 12 }} dy={11}/><Tooltip formatter={(value) => currency(Number(value))} contentStyle={{ border: '1px solid #e9ece8', borderRadius: 12, fontSize: 12, boxShadow: '0 5px 18px #19221a12' }}/><Area type="monotone" dataKey="expenses" name="Spent" stroke="#569c7d" strokeWidth={2.5} fill="url(#spendFill)" dot={false} activeDot={{ r: 5, fill: '#fff', stroke: '#569c7d', strokeWidth: 2 }}/></AreaChart></ResponsiveContainer></div></article>
            <article className="panel budget-panel"><div className="panel-heading"><div><h2>Spending plan</h2><p>{monthLabel}</p></div><button className="more-button" onClick={() => setTab('Budgets')} aria-label="Manage budgets"><Ellipsis size={20}/></button></div><div className="budget-total"><b>{currency(actualBudget)}</b><span>of {currency(totalBudget)}</span></div><div className="budget-progress"><i style={{ width: `${budgetPercent}%`, background: budgetRemaining < 0 ? '#ce927d' : '#75af90' }}/></div><div className="budget-left"><span><b>{currency(Math.abs(budgetRemaining))}</b> {budgetRemaining >= 0 ? 'left this month' : 'over budget'}</span><span>{Math.round(totalBudget ? actualBudget / totalBudget * 100 : 0)}%</span></div><div className="budget-categories">{budgets.slice(0, 3).map((budget, i) => <div className="budget-category" key={budget.id}><i className={`category-dot ${['dot-food', 'dot-home', 'dot-life'][i % 3]}`}/><span>{budget.name}</span><b>{currency(budget.actual)} <small>/ {currency(budget.limit)}</small></b><span className="cat-progress"><i style={{ width: `${Math.min(100, budget.percentUsed)}%`, background: ['#80bca2', '#b4a8dc', '#e7af86'][i % 3] }}/></span></div>)}{budgets.length === 0 && <p className="empty-inline">No budgets for this month yet.</p>}</div><button className="all-budgets" onClick={() => setTab('Budgets')}>See your full plan <ArrowRight size={15}/></button></article></section>
          <section className="bottom-grid"><article className="panel transactions-panel"><div className="panel-heading"><div><h2>Recent activity</h2><p>Your latest money moves.</p></div><button className="text-link" onClick={() => setTab('Transactions')}>All activity <ArrowRight size={15}/></button></div><div className="transactions-list">{dashboard.recent.slice(0, 4).map(tx => <TransactionRow key={`${tx.type}-${tx.id}`} tx={tx} onEdit={() => openTransaction(tx)}/>) }{dashboard.recent.length === 0 && <p className="empty-inline">Your saved transactions will appear here.</p>}</div></article>
            <article className="goal-card"><div className="goal-top"><span className="goal-icon"><Target size={18}/></span><button className="goal-more" aria-label="View savings goals" onClick={() => setTab('Savings goals')}><Ellipsis size={20}/></button></div>{goals[0] ? <><span className="goal-label">SAVING FOR</span><h2>{goals[0].name}</h2><p>{goals[0].targetDate ? `Target date ${prettyDate(goals[0].targetDate)}` : 'A little progress adds up.'}</p><div className="goal-amount"><b>{currency(goals[0].currentAmount)}</b><span> of {currency(goals[0].targetAmount)}</span></div><div className="goal-progress"><i style={{ width: `${Math.min(100, goals[0].percentComplete)}%` }}/></div><div className="goal-footer"><span><b>{Math.round(goals[0].percentComplete)}%</b> there</span><span>{goals.length} savings {goals.length === 1 ? 'goal' : 'goals'}</span></div></> : <><span className="goal-label">YOUR NEXT GOAL</span><h2>No savings goals yet</h2><p>Set a target and track your progress.</p></>}<button className="goal-action" onClick={() => setTab('Savings goals')}>View your goals <ArrowRight size={15}/></button></article></section>
        </> : tab === 'Transactions' ? <section className="panel data-panel"><div className="section-heading"><div><h2>Transactions</h2><p>Income and spending saved to your account.</p></div><button className="primary-button" onClick={() => openTransaction()}><Plus size={16}/> Add transaction</button></div><div className="transactions-list full-list">{transactions.map(tx => <TransactionRow key={`${tx.type}-${tx.id}`} tx={tx} onEdit={() => openTransaction(tx)} onDelete={() => window.confirm('Delete this transaction?') && runAction(() => moneyApi.deleteTransaction(token, tx), 'Transaction deleted')}/>) }{transactions.length === 0 && <EmptyState text="No transactions yet. Add your first income or expense."/>}</div></section>
        : tab === 'Budgets' ? <section className="panel data-panel"><div className="section-heading"><div><h2>Budgets</h2><p>Set category limits and compare them with actual spending for {monthLabel}.</p></div><button className="primary-button" onClick={() => { setEditingBudget(null); setBudgetForm({ name: '', limit: '', year: Number(period.slice(0, 4)), month: Number(period.slice(5, 7)) }); setBudgetFormOpen(!budgetFormOpen) }}><Plus size={16}/> Add budget</button></div>{budgetFormOpen && <form className="inline-form" onSubmit={submitBudget}><label>Category<input value={budgetForm.name} onChange={e => setBudgetForm({ ...budgetForm, name: e.target.value })} maxLength={60} required placeholder="e.g. Food"/></label><label>Monthly limit<input type="number" min="0.01" step="0.01" value={budgetForm.limit} onChange={e => setBudgetForm({ ...budgetForm, limit: e.target.value })} required/></label><label>Month<input type="month" value={`${budgetForm.year}-${String(budgetForm.month).padStart(2, '0')}`} onChange={e => { const [year, month] = e.target.value.split('-').map(Number); setBudgetForm({ ...budgetForm, year, month }) }} required/></label><div className="form-actions"><button type="button" className="secondary-button" onClick={() => { setBudgetFormOpen(false); setEditingBudget(null) }}>Cancel</button><button className="primary-button" disabled={saving}>{editingBudget ? 'Save changes' : 'Create budget'}</button></div></form>}<div className="budget-list">{budgets.map(budget => <article className="budget-detail" key={budget.id}><div className="budget-detail-top"><span className="category-dot dot-food"/><div className="grow"><h3>{budget.name}</h3><small>{new Date(budget.year, budget.month - 1, 1).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' })}</small></div><span className="budget-percent">{Math.round(budget.percentUsed)}% used</span><button className="secondary-button small-button" onClick={() => editBudget(budget)}>Edit</button><button className="danger-button" onClick={() => window.confirm(`Delete the ${budget.name} budget?`) && runAction(() => moneyApi.deleteBudget(token, budget.id), 'Budget deleted')}>Delete</button></div><div className="budget-progress"><i style={{ width: `${Math.min(100, budget.percentUsed)}%`, background: budget.remaining < 0 ? '#ce927d' : '#75af90' }}/></div><div className="budget-metrics"><span>Spent <b>{currency(budget.actual)}</b></span><span>Budget <b>{currency(budget.limit)}</b></span><span>{budget.remaining >= 0 ? 'Remaining' : 'Over'} <b>{currency(Math.abs(budget.remaining))}</b></span></div></article>)}{budgets.length === 0 && <EmptyState text="No budget is set for this month. Create a category budget to track spending."/>}</div></section>
        : tab === 'Savings goals' ? <section className="panel data-panel"><div className="section-heading"><div><h2>Savings goals</h2><p>Track the amount set aside toward each target.</p></div><button className="primary-button" onClick={() => { setEditingGoal(null); setGoalForm({ name: '', targetAmount: '', currentAmount: '0', targetDate: '', monthlyContribution: '0' }); setGoalFormOpen(!goalFormOpen) }}><Plus size={16}/> Add goal</button></div>{goalFormOpen && <form className="inline-form" onSubmit={submitGoal}><label>Goal name<input value={goalForm.name} onChange={e => setGoalForm({ ...goalForm, name: e.target.value })} required maxLength={100}/></label><label>Target amount<input type="number" min="0.01" step="0.01" value={goalForm.targetAmount} onChange={e => setGoalForm({ ...goalForm, targetAmount: e.target.value })} required/></label><label>Current amount<input type="number" min="0" step="0.01" value={goalForm.currentAmount} onChange={e => setGoalForm({ ...goalForm, currentAmount: e.target.value })} required/></label><label>Target date<input type="date" value={goalForm.targetDate} onChange={e => setGoalForm({ ...goalForm, targetDate: e.target.value })}/></label><label>Monthly contribution<input type="number" min="0" step="0.01" value={goalForm.monthlyContribution} onChange={e => setGoalForm({ ...goalForm, monthlyContribution: e.target.value })} required/></label><div className="form-actions"><button type="button" className="secondary-button" onClick={() => { setGoalFormOpen(false); setEditingGoal(null) }}>Cancel</button><button className="primary-button" disabled={saving}>{editingGoal ? 'Save changes' : 'Create goal'}</button></div></form>}<div className="goal-grid">{goals.map(goal => <article className="goal-detail" key={goal.id}><div className="goal-top"><span className="goal-icon"><Target size={18}/></span><span className="goal-percent">{Math.round(goal.percentComplete)}%</span></div><h3>{goal.name}</h3><p>{goal.targetDate ? `Target ${prettyDate(goal.targetDate)}` : 'No target date'}</p><div className="goal-amount"><b>{currency(goal.currentAmount)}</b><span> of {currency(goal.targetAmount)}</span></div><div className="goal-progress"><i style={{ width: `${Math.min(100, goal.percentComplete)}%` }}/></div><div className="goal-footer"><span>Monthly plan <b>{currency(goal.monthlyContribution)}</b></span><span>{currency(Math.max(0, goal.targetAmount - goal.currentAmount))} to go</span></div><div className="row-actions"><button className="secondary-button small-button" onClick={() => editGoal(goal)}>Edit goal</button><button className="danger-button" onClick={() => window.confirm(`Delete ${goal.name}?`) && runAction(() => moneyApi.deleteGoal(token, goal.id), 'Savings goal deleted')}>Delete</button></div></article>)}{goals.length === 0 && <EmptyState text="Create a savings goal to see your progress here."/>}</div></section>
        : <section className="panel data-panel"><div className="section-heading"><div><h2>Payments & debts</h2><p>Track outstanding balances and payments against them.</p></div><button className="primary-button" onClick={() => { setEditingDebt(null); setDebtForm({ name: '', originalAmount: '', balance: '', interestRate: '0', minimumPayment: '0', frequency: 'Monthly', nextDueDate: '', status: 'Active' }); setDebtFormOpen(!debtFormOpen) }}><Plus size={16}/> Add debt</button></div>{debtFormOpen && <form className="inline-form" onSubmit={submitDebt}><label>Debt name<input value={debtForm.name} onChange={e => setDebtForm({ ...debtForm, name: e.target.value })} maxLength={100} required placeholder="e.g. Credit card"/></label><label>Original amount<input type="number" min="0.01" step="0.01" value={debtForm.originalAmount} onChange={e => setDebtForm({ ...debtForm, originalAmount: e.target.value, balance: editingDebt ? debtForm.balance : e.target.value })} required/></label><label>Current balance<input type="number" min="0" step="0.01" value={debtForm.balance} onChange={e => setDebtForm({ ...debtForm, balance: e.target.value })} required/></label><label>Interest rate (%)<input type="number" min="0" max="100" step="0.001" value={debtForm.interestRate} onChange={e => setDebtForm({ ...debtForm, interestRate: e.target.value })} required/></label><label>Minimum payment<input type="number" min="0" step="0.01" value={debtForm.minimumPayment} onChange={e => setDebtForm({ ...debtForm, minimumPayment: e.target.value })} required/></label><label>Payment frequency<select value={debtForm.frequency} onChange={e => setDebtForm({ ...debtForm, frequency: e.target.value })}><option>Monthly</option><option>Weekly</option><option>Fortnightly</option></select></label><label>Next due date<input type="date" value={debtForm.nextDueDate} onChange={e => setDebtForm({ ...debtForm, nextDueDate: e.target.value })}/></label><label>Status<input value={debtForm.status} onChange={e => setDebtForm({ ...debtForm, status: e.target.value })} maxLength={30} required/></label><div className="form-actions"><button type="button" className="secondary-button" onClick={() => { setDebtFormOpen(false); setEditingDebt(null) }}>Cancel</button><button className="primary-button" disabled={saving}>{editingDebt ? 'Save changes' : 'Add debt'}</button></div></form>}<div className="debt-list">{debts.map(debt => <article className="debt-detail" key={debt.id}><div className="section-heading"><div><h3>{debt.name}</h3><p>{debt.interestRate}% interest · {debt.frequency} · {debt.status}{debt.nextDueDate ? ` · Due ${prettyDate(debt.nextDueDate)}` : ''}</p></div><div className="row-actions"><button className="secondary-button small-button" onClick={() => editDebt(debt)}>Edit</button><button className="danger-button" onClick={() => window.confirm(`Delete ${debt.name} and its payment history?`) && runAction(() => moneyApi.deleteDebt(token, debt.id), 'Debt deleted')}>Delete</button></div></div><div className="budget-metrics"><span>Balance <b>{currency(debt.balance)}</b></span><span>Original <b>{currency(debt.originalAmount)}</b></span><span>Minimum payment <b>{currency(debt.minimumPayment)}</b></span></div><div className="payment-list"><div className="payment-heading"><b>Payment history</b><button className="text-link" onClick={() => openPayment(debt.id)}><Plus size={14}/> Record payment</button></div>{debt.payments.map(payment => <div className="payment-row" key={payment.id}><span>{prettyDate(payment.date)}{payment.notes ? ` · ${payment.notes}` : ''}</span><b>{currency(payment.amount)}</b><span className="row-actions"><button className="text-link" onClick={() => openPayment(debt.id, payment)}>Edit</button><button className="danger-button" onClick={() => window.confirm('Delete this payment and restore the debt balance?') && runAction(() => moneyApi.deletePayment(token, debt.id, payment.id), 'Payment deleted')}>Delete</button></span></div>)}{debt.payments.length === 0 && <p className="empty-inline">No payments recorded yet.</p>}{paymentTarget === debt.id && <form className="inline-form payment-form" onSubmit={submitPayment}><label>Amount<input type="number" min="0.01" max={debt.balance + (editingPayment ? debt.payments.find(x => x.id === editingPayment)?.amount ?? 0 : 0)} step="0.01" value={paymentForm.amount} onChange={e => setPaymentForm({ ...paymentForm, amount: e.target.value })} required/></label><label>Date<input type="date" value={paymentForm.date} onChange={e => setPaymentForm({ ...paymentForm, date: e.target.value })} required/></label><label>Notes<input value={paymentForm.notes} onChange={e => setPaymentForm({ ...paymentForm, notes: e.target.value })} maxLength={2000}/></label><div className="form-actions"><button type="button" className="secondary-button" onClick={() => setPaymentTarget(null)}>Cancel</button><button className="primary-button" disabled={saving}>{editingPayment ? 'Save payment' : 'Record payment'}</button></div></form>}</div></article>)}{debts.length === 0 && <EmptyState text="Add a debt to track its balance and payments."/>}</div></section>}
        <footer className="page-footer"><span>One step at a time. Your numbers update as you go.</span><span>MoneyBoard <i>✳</i></span></footer></div>
    </main>
    {transactionModal && <div className="modal-backdrop" onMouseDown={e => { if (e.target === e.currentTarget) setTransactionModal(false) }}><form className="transaction-modal" onSubmit={submitTransaction}><div className="modal-heading"><div><span className="eyebrow">A LITTLE HOUSEKEEPING</span><h2>{editingTransaction ? 'Edit transaction' : 'Add a transaction'}</h2></div><button type="button" className="icon-button" onClick={() => setTransactionModal(false)} aria-label="Close"><X size={19}/></button></div><div className="kind-toggle"><button type="button" disabled={!!editingTransaction} className={transactionForm.type === 'expense' ? 'selected' : ''} onClick={() => setTransactionForm({ ...transactionForm, type: 'expense' })}>Money out</button><button type="button" disabled={!!editingTransaction} className={transactionForm.type === 'income' ? 'selected' : ''} onClick={() => setTransactionForm({ ...transactionForm, type: 'income' })}>Money in</button></div><label>{transactionForm.type === 'income' ? 'Income source' : 'What was it for?'}<input autoFocus placeholder={transactionForm.type === 'income' ? 'e.g. Monthly salary' : 'e.g. Coffee with a friend'} value={transactionForm.title} onChange={e => setTransactionForm({ ...transactionForm, title: e.target.value })} required maxLength={100}/></label><div className="form-grid"><label>Amount<input type="number" min="0.01" step="0.01" placeholder="0.00" value={transactionForm.amount} onChange={e => setTransactionForm({ ...transactionForm, amount: e.target.value })} required/></label><label>Date<input type="date" value={transactionForm.date} onChange={e => setTransactionForm({ ...transactionForm, date: e.target.value })} required/></label></div>{transactionForm.type === 'expense' && <label>Category<select value={transactionForm.category} onChange={e => setTransactionForm({ ...transactionForm, category: e.target.value })}>{categories.map(category => <option key={category}>{category}</option>)}</select></label>}<label>Notes <span className="optional-label">optional</span><textarea rows={3} maxLength={2000} value={transactionForm.notes} onChange={e => setTransactionForm({ ...transactionForm, notes: e.target.value })}/></label><button className="primary-button modal-submit" type="submit" disabled={saving}>{saving ? 'Saving…' : editingTransaction ? 'Save changes' : 'Save transaction'} <ArrowRight size={16}/></button><p className="modal-hint">Saved securely to your account.</p></form></div>}
    {notice && <div className="toast"><span>✓</span>{notice}<button onClick={() => setNotice('')} aria-label="Dismiss"><X size={14}/></button></div>}
  </div>
}

function TransactionRow({ tx, onEdit, onDelete }: { tx: Transaction; onEdit: () => void; onDelete?: () => void }) {
  const Icon = tx.type === 'income' ? ArrowDownLeft : Leaf
  return <div className="transaction-row"><span className={`transaction-icon ${tx.type === 'income' ? 'mint' : 'lavender'}`}><Icon size={17}/></span><span className="transaction-info"><b>{tx.title}</b><small>{tx.category} · {prettyDate(tx.date)}{tx.notes ? ` · ${tx.notes}` : ''}</small></span><span className={`transaction-amount ${tx.type === 'income' ? 'positive' : ''}`}>{tx.type === 'income' ? '+' : '−'}{currency(tx.amount)}</span><span className="row-actions"><button className="text-link" onClick={onEdit}>Edit</button>{onDelete && <button className="danger-button" onClick={onDelete}>Delete</button>}</span></div>
}

function EmptyState({ text }: { text: string }) { return <div className="empty-state"><span className="goal-icon"><Wallet size={18}/></span><p>{text}</p></div> }

export default App
