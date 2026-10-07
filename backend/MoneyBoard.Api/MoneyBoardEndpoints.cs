using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyBoard.Api.Data;
using MoneyBoard.Api.Models;

namespace MoneyBoard.Api;

public static class MoneyBoardEndpoints
{
    private const decimal MaximumAmount = 9_999_999_999.99m;

    public static IEndpointRouteBuilder MapMoneyBoardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").RequireAuthorization();

        api.MapGet("/dashboard", GetDashboard);
        api.MapGet("/transactions", GetTransactions);
        api.MapPost("/transactions", CreateTransaction);
        api.MapPut("/transactions/{type}/{id:guid}", UpdateTransaction);
        api.MapDelete("/transactions/{type}/{id:guid}", DeleteTransaction);

        api.MapGet("/budgets", GetBudgets);
        api.MapPost("/budgets", CreateBudget);
        api.MapPut("/budgets/{id:guid}", UpdateBudget);
        api.MapDelete("/budgets/{id:guid}", DeleteBudget);

        api.MapGet("/savings-goals", GetSavingsGoals);
        api.MapPost("/savings-goals", CreateSavingsGoal);
        api.MapPut("/savings-goals/{id:guid}", UpdateSavingsGoal);
        api.MapDelete("/savings-goals/{id:guid}", DeleteSavingsGoal);

        api.MapGet("/debts", GetDebts);
        api.MapPost("/debts", CreateDebt);
        api.MapPut("/debts/{id:guid}", UpdateDebt);
        api.MapDelete("/debts/{id:guid}", DeleteDebt);
        api.MapPost("/debts/{id:guid}/payments", CreateDebtPayment);
        api.MapPut("/debts/{id:guid}/payments/{paymentId:guid}", UpdateDebtPayment);
        api.MapDelete("/debts/{id:guid}/payments/{paymentId:guid}", DeleteDebtPayment);

        return endpoints;
    }

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")!);

    private static async Task<IResult> GetDashboard(ClaimsPrincipal principal, MoneyBoardDbContext db, int? year, int? month)
    {
        var userId = UserId(principal);
        var now = DateTime.UtcNow;
        var selectedYear = year ?? now.Year;
        var selectedMonth = month ?? now.Month;
        if (selectedMonth is < 1 or > 12 || selectedYear is < 2000 or > 2200)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["period"] = ["Choose a valid month and year."] });

        var periodStart = new DateOnly(selectedYear, selectedMonth, 1);
        var periodEnd = periodStart.AddMonths(1);
        var income = await db.Incomes.AsNoTracking().Where(x => x.UserId == userId && x.Date >= periodStart && x.Date < periodEnd).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var expenses = await db.Expenses.AsNoTracking().Where(x => x.UserId == userId && x.Date >= periodStart && x.Date < periodEnd).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var allIncome = await db.Incomes.AsNoTracking().Where(x => x.UserId == userId).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var allExpenses = await db.Expenses.AsNoTracking().Where(x => x.UserId == userId).SumAsync(x => (decimal?)x.Amount) ?? 0;

        var budgetRows = await db.Budgets.AsNoTracking().Where(x => x.UserId == userId && x.Year == selectedYear && x.Month == selectedMonth).ToListAsync();
        var spendingByCategory = await db.Expenses.AsNoTracking()
            .Where(x => x.UserId == userId && x.Date >= periodStart && x.Date < periodEnd)
            .GroupBy(x => x.Category!.Name)
            .Select(group => new { Category = group.Key, Amount = group.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Category, x => x.Amount, StringComparer.OrdinalIgnoreCase);
        var budgets = budgetRows.Select(x => ToBudgetView(x, spendingByCategory.GetValueOrDefault(x.Name))).ToArray();

        var goalValues = await db.SavingsGoals.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.CurrentAmount, x.TargetAmount }).ToListAsync();
        var recent = await ReadTransactions(db, userId);

        var firstTrendMonth = periodStart.AddMonths(-5);
        var incomeTrend = await db.Incomes.AsNoTracking().Where(x => x.UserId == userId && x.Date >= firstTrendMonth && x.Date < periodEnd)
            .GroupBy(x => new { x.Date.Year, x.Date.Month }).Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) }).ToListAsync();
        var expenseTrend = await db.Expenses.AsNoTracking().Where(x => x.UserId == userId && x.Date >= firstTrendMonth && x.Date < periodEnd)
            .GroupBy(x => new { x.Date.Year, x.Date.Month }).Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) }).ToListAsync();
        var trend = Enumerable.Range(0, 6).Select(offset =>
        {
            var date = firstTrendMonth.AddMonths(offset);
            return new TrendView(date.ToString("MMM"), incomeTrend.Where(x => x.Year == date.Year && x.Month == date.Month).Sum(x => x.Amount), expenseTrend.Where(x => x.Year == date.Year && x.Month == date.Month).Sum(x => x.Amount));
        }).ToArray();

        return Results.Ok(new DashboardView(selectedYear, selectedMonth, allIncome - allExpenses, income, expenses, goalValues.Sum(x => x.CurrentAmount), goalValues.Sum(x => x.TargetAmount), budgets, trend, recent.Take(8).ToArray()));
    }

    private static async Task<List<TransactionView>> ReadTransactions(MoneyBoardDbContext db, Guid userId)
    {
        var incomes = await db.Incomes.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new TransactionView(x.Id, "income", x.Source, x.Amount, x.Date, "Income", x.Notes, x.CreatedAt)).ToListAsync();
        var expenses = await db.Expenses.AsNoTracking().Where(x => x.UserId == userId).Include(x => x.Category)
            .Select(x => new TransactionView(x.Id, "expense", x.Description, x.Amount, x.Date, x.Category!.Name, x.Notes, x.CreatedAt)).ToListAsync();
        return incomes.Concat(expenses).OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt).ToList();
    }

    private static async Task<IResult> GetTransactions(ClaimsPrincipal principal, MoneyBoardDbContext db) =>
        Results.Ok(await ReadTransactions(db, UserId(principal)));

    private static async Task<IResult> CreateTransaction(ClaimsPrincipal principal, MoneyBoardDbContext db, TransactionInput input)
    {
        var error = ValidateTransaction(input);
        if (error is not null) return error;
        var userId = UserId(principal);
        if (input.Type.Equals("income", StringComparison.OrdinalIgnoreCase))
        {
            var income = new Income { UserId = userId, Source = input.Title.Trim(), Amount = input.Amount, Date = input.Date, Notes = TrimToNull(input.Notes) };
            db.Incomes.Add(income);
            await db.SaveChangesAsync();
            return Results.Created($"/api/transactions/income/{income.Id}", new TransactionView(income.Id, "income", income.Source, income.Amount, income.Date, "Income", income.Notes, income.CreatedAt));
        }

        var category = await FindOrCreateCategory(db, userId, input.Category!);
        var expense = new Expense { UserId = userId, Description = input.Title.Trim(), Amount = input.Amount, Date = input.Date, Notes = TrimToNull(input.Notes), CategoryId = category.Id };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        return Results.Created($"/api/transactions/expense/{expense.Id}", new TransactionView(expense.Id, "expense", expense.Description, expense.Amount, expense.Date, category.Name, expense.Notes, expense.CreatedAt));
    }

    private static async Task<IResult> UpdateTransaction(ClaimsPrincipal principal, MoneyBoardDbContext db, string type, Guid id, TransactionInput input)
    {
        var error = ValidateTransaction(input);
        if (error is not null) return error;
        var userId = UserId(principal);
        if (type.Equals("income", StringComparison.OrdinalIgnoreCase))
        {
            var income = await db.Incomes.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id);
            if (income is null) return Results.NotFound();
            income.Source = input.Title.Trim(); income.Amount = input.Amount; income.Date = input.Date; income.Notes = TrimToNull(input.Notes); income.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new TransactionView(income.Id, "income", income.Source, income.Amount, income.Date, "Income", income.Notes, income.CreatedAt));
        }
        if (!type.Equals("expense", StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
        var expense = await db.Expenses.Include(x => x.Category).SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (expense is null) return Results.NotFound();
        var category = await FindOrCreateCategory(db, userId, input.Category!);
        expense.Description = input.Title.Trim(); expense.Amount = input.Amount; expense.Date = input.Date; expense.Notes = TrimToNull(input.Notes); expense.CategoryId = category.Id; expense.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new TransactionView(expense.Id, "expense", expense.Description, expense.Amount, expense.Date, category.Name, expense.Notes, expense.CreatedAt));
    }

    private static async Task<IResult> DeleteTransaction(ClaimsPrincipal principal, MoneyBoardDbContext db, string type, Guid id)
    {
        var userId = UserId(principal);
        if (type.Equals("income", StringComparison.OrdinalIgnoreCase))
        {
            var income = await db.Incomes.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id);
            if (income is null) return Results.NotFound();
            db.Remove(income);
        }
        else if (type.Equals("expense", StringComparison.OrdinalIgnoreCase))
        {
            var expense = await db.Expenses.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id);
            if (expense is null) return Results.NotFound();
            db.Remove(expense);
        }
        else return Results.NotFound();
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static IResult? ValidateTransaction(TransactionInput input)
    {
        if (!input.Type.Equals("income", StringComparison.OrdinalIgnoreCase) && !input.Type.Equals("expense", StringComparison.OrdinalIgnoreCase)) return FieldError("type", "Type must be income or expense.");
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 100) return FieldError("title", "Enter a description of up to 100 characters.");
        if (input.Amount <= 0 || input.Amount > MaximumAmount || decimal.Round(input.Amount, 2) != input.Amount) return FieldError("amount", "Enter an amount between 0.01 and 9,999,999,999.99 with at most two decimal places.");
        if (input.Date == default) return FieldError("date", "Choose a valid transaction date.");
        if (input.Type.Equals("expense", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(input.Category) || input.Category.Trim().Length > 60)) return FieldError("category", "Choose a category of up to 60 characters.");
        if (input.Notes?.Length > 2000) return FieldError("notes", "Notes must be 2,000 characters or fewer.");
        return null;
    }

    private static async Task<ExpenseCategory> FindOrCreateCategory(MoneyBoardDbContext db, Guid userId, string name)
    {
        var trimmed = name.Trim();
        var category = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.UserId == userId && x.Name.ToLower() == trimmed.ToLower());
        if (category is not null) return category;
        category = new ExpenseCategory { UserId = userId, Name = trimmed };
        db.ExpenseCategories.Add(category);
        return category;
    }

    private static async Task<IResult> GetBudgets(ClaimsPrincipal principal, MoneyBoardDbContext db, int? year, int? month)
    {
        var now = DateTime.UtcNow;
        var selectedYear = year ?? now.Year; var selectedMonth = month ?? now.Month;
        if (selectedMonth is < 1 or > 12 || selectedYear is < 2000 or > 2200) return Results.ValidationProblem(new Dictionary<string, string[]> { ["period"] = ["Choose a valid month and year."] });
        var start = new DateOnly(selectedYear, selectedMonth, 1); var end = start.AddMonths(1);
        var rows = await db.Budgets.AsNoTracking().Where(x => x.UserId == UserId(principal) && x.Year == selectedYear && x.Month == selectedMonth).OrderBy(x => x.Name).ToListAsync();
        var actuals = await db.Expenses.AsNoTracking().Where(x => x.UserId == UserId(principal) && x.Date >= start && x.Date < end)
            .GroupBy(x => x.Category!.Name).Select(g => new { Name = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Name, x => x.Amount, StringComparer.OrdinalIgnoreCase);
        return Results.Ok(rows.Select(x => ToBudgetView(x, actuals.GetValueOrDefault(x.Name))));
    }

    private static async Task<IResult> CreateBudget(ClaimsPrincipal principal, MoneyBoardDbContext db, BudgetInput input)
    {
        var error = ValidateBudget(input); if (error is not null) return error;
        var userId = UserId(principal); var name = input.Name.Trim();
        if (await db.Budgets.AnyAsync(x => x.UserId == userId && x.Year == input.Year && x.Month == input.Month && x.Name == name)) return Results.Conflict(new { error = "A budget already exists for this category and month." });
        var budget = new Budget { UserId = userId, Name = name, Limit = input.Limit, Year = input.Year, Month = input.Month };
        db.Budgets.Add(budget); await db.SaveChangesAsync();
        return Results.Created($"/api/budgets/{budget.Id}", ToBudgetView(budget, 0));
    }

    private static async Task<IResult> UpdateBudget(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id, BudgetInput input)
    {
        var error = ValidateBudget(input); if (error is not null) return error;
        var userId = UserId(principal); var budget = await db.Budgets.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (budget is null) return Results.NotFound();
        var name = input.Name.Trim();
        if (await db.Budgets.AnyAsync(x => x.UserId == userId && x.Id != id && x.Year == input.Year && x.Month == input.Month && x.Name == name)) return Results.Conflict(new { error = "A budget already exists for this category and month." });
        budget.Name = name; budget.Limit = input.Limit; budget.Year = input.Year; budget.Month = input.Month; budget.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var start = new DateOnly(budget.Year, budget.Month, 1); var end = start.AddMonths(1);
        var actual = await db.Expenses.Where(x => x.UserId == userId && x.Category!.Name == budget.Name && x.Date >= start && x.Date < end).SumAsync(x => (decimal?)x.Amount) ?? 0;
        return Results.Ok(ToBudgetView(budget, actual));
    }

    private static async Task<IResult> DeleteBudget(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id)
    {
        var budget = await db.Budgets.SingleOrDefaultAsync(x => x.UserId == UserId(principal) && x.Id == id);
        if (budget is null) return Results.NotFound();
        db.Remove(budget); await db.SaveChangesAsync(); return Results.NoContent();
    }

    private static IResult? ValidateBudget(BudgetInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 60) return FieldError("name", "Enter a category name of up to 60 characters.");
        if (input.Limit <= 0 || input.Limit > MaximumAmount || decimal.Round(input.Limit, 2) != input.Limit) return FieldError("limit", "Enter a positive monthly amount with at most two decimal places.");
        return input.Month is < 1 or > 12 || input.Year is < 2000 or > 2200 ? FieldError("period", "Choose a valid month and year.") : null;
    }

    private static async Task<IResult> GetSavingsGoals(ClaimsPrincipal principal, MoneyBoardDbContext db)
    {
        var goals = await db.SavingsGoals.AsNoTracking().Where(x => x.UserId == UserId(principal)).OrderBy(x => x.TargetDate).ThenBy(x => x.Name).ToListAsync();
        return Results.Ok(goals.Select(GoalView));
    }

    private static async Task<IResult> CreateSavingsGoal(ClaimsPrincipal principal, MoneyBoardDbContext db, SavingsGoalInput input)
    {
        var error = ValidateGoal(input); if (error is not null) return error;
        var goal = new SavingsGoal { UserId = UserId(principal), Name = input.Name.Trim(), TargetAmount = input.TargetAmount, CurrentAmount = input.CurrentAmount, TargetDate = input.TargetDate, MonthlyContribution = input.MonthlyContribution };
        db.SavingsGoals.Add(goal); await db.SaveChangesAsync(); return Results.Created($"/api/savings-goals/{goal.Id}", GoalView(goal));
    }

    private static async Task<IResult> UpdateSavingsGoal(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id, SavingsGoalInput input)
    {
        var error = ValidateGoal(input); if (error is not null) return error;
        var goal = await db.SavingsGoals.SingleOrDefaultAsync(x => x.UserId == UserId(principal) && x.Id == id);
        if (goal is null) return Results.NotFound();
        goal.Name = input.Name.Trim(); goal.TargetAmount = input.TargetAmount; goal.CurrentAmount = input.CurrentAmount; goal.TargetDate = input.TargetDate; goal.MonthlyContribution = input.MonthlyContribution; goal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Results.Ok(GoalView(goal));
    }

    private static async Task<IResult> DeleteSavingsGoal(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id)
    {
        var goal = await db.SavingsGoals.SingleOrDefaultAsync(x => x.UserId == UserId(principal) && x.Id == id);
        if (goal is null) return Results.NotFound();
        db.Remove(goal); await db.SaveChangesAsync(); return Results.NoContent();
    }

    private static IResult? ValidateGoal(SavingsGoalInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100) return FieldError("name", "Enter a goal name of up to 100 characters.");
        if (input.TargetAmount <= 0 || input.TargetAmount > MaximumAmount || input.CurrentAmount < 0 || input.CurrentAmount > MaximumAmount || input.MonthlyContribution < 0 || input.MonthlyContribution > MaximumAmount) return FieldError("amount", "Enter a positive target and non-negative current and monthly amounts.");
        if (decimal.Round(input.TargetAmount, 2) != input.TargetAmount || decimal.Round(input.CurrentAmount, 2) != input.CurrentAmount || decimal.Round(input.MonthlyContribution, 2) != input.MonthlyContribution) return FieldError("amount", "Amounts can have at most two decimal places.");
        if (input.TargetDate == DateOnly.MinValue) return FieldError("targetDate", "Choose a valid target date.");
        return null;
    }

    private static async Task<IResult> GetDebts(ClaimsPrincipal principal, MoneyBoardDbContext db)
    {
        var debts = await db.Debts.AsNoTracking().Where(x => x.UserId == UserId(principal)).Include(x => x.Payments).OrderBy(x => x.NextDueDate).ThenBy(x => x.Name).ToListAsync();
        return Results.Ok(debts.Select(ToDebtView));
    }

    private static async Task<IResult> CreateDebt(ClaimsPrincipal principal, MoneyBoardDbContext db, DebtInput input)
    {
        var error = ValidateDebt(input); if (error is not null) return error;
        var debt = new Debt { UserId = UserId(principal), Name = input.Name.Trim(), OriginalAmount = input.OriginalAmount, Balance = input.Balance, InterestRate = input.InterestRate, MinimumPayment = input.MinimumPayment, Frequency = input.Frequency.Trim(), NextDueDate = input.NextDueDate, Status = input.Status.Trim() };
        db.Debts.Add(debt); await db.SaveChangesAsync(); return Results.Created($"/api/debts/{debt.Id}", ToDebtView(debt));
    }

    private static async Task<IResult> UpdateDebt(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id, DebtInput input)
    {
        var error = ValidateDebt(input); if (error is not null) return error;
        var debt = await db.Debts.Include(x => x.Payments).SingleOrDefaultAsync(x => x.UserId == UserId(principal) && x.Id == id);
        if (debt is null) return Results.NotFound();
        var totalPayments = debt.Payments.Sum(x => x.Amount);
        if (input.Balance < 0 || input.Balance > input.OriginalAmount || input.OriginalAmount < totalPayments) return FieldError("balance", "The original amount and balance must include existing payments.");
        debt.Name = input.Name.Trim(); debt.OriginalAmount = input.OriginalAmount; debt.Balance = input.Balance; debt.InterestRate = input.InterestRate; debt.MinimumPayment = input.MinimumPayment; debt.Frequency = input.Frequency.Trim(); debt.NextDueDate = input.NextDueDate; debt.Status = input.Status.Trim(); debt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Results.Ok(ToDebtView(debt));
    }

    private static async Task<IResult> DeleteDebt(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id)
    {
        var debt = await db.Debts.Include(x => x.Payments).SingleOrDefaultAsync(x => x.UserId == UserId(principal) && x.Id == id);
        if (debt is null) return Results.NotFound();
        db.Remove(debt); await db.SaveChangesAsync(); return Results.NoContent();
    }

    private static IResult? ValidateDebt(DebtInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100) return FieldError("name", "Enter a debt name of up to 100 characters.");
        if (input.OriginalAmount <= 0 || input.Balance < 0 || input.Balance > input.OriginalAmount || input.InterestRate < 0 || input.InterestRate > 100 || input.MinimumPayment < 0) return FieldError("amount", "Check the debt amounts and interest rate.");
        if (input.Frequency.Trim().Length is < 1 or > 30 || input.Status.Trim().Length is < 1 or > 30) return FieldError("details", "Frequency and status must be between 1 and 30 characters.");
        return null;
    }

    private static async Task<IResult> CreateDebtPayment(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id, DebtPaymentInput input)
    {
        if (input.Amount <= 0 || input.Amount > MaximumAmount || decimal.Round(input.Amount, 2) != input.Amount || input.Date == default || input.Notes?.Length > 2000) return FieldError("payment", "Enter a valid payment amount, date, and notes.");
        var userId = UserId(principal); var debt = await db.Debts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (debt is null) return Results.NotFound();
        if (input.Amount > debt.Balance) return FieldError("amount", "A payment cannot exceed the remaining debt balance.");
        var payment = new DebtPayment { UserId = userId, DebtId = id, Amount = input.Amount, Date = input.Date, Notes = TrimToNull(input.Notes) };
        debt.Balance -= input.Amount; debt.UpdatedAt = DateTime.UtcNow; db.DebtPayments.Add(payment); await db.SaveChangesAsync();
        return Results.Created($"/api/debts/{id}/payments/{payment.Id}", PaymentView(payment));
    }

    private static async Task<IResult> UpdateDebtPayment(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id, Guid paymentId, DebtPaymentInput input)
    {
        if (input.Amount <= 0 || input.Amount > MaximumAmount || decimal.Round(input.Amount, 2) != input.Amount || input.Date == default || input.Notes?.Length > 2000) return FieldError("payment", "Enter a valid payment amount, date, and notes.");
        var userId = UserId(principal); var debt = await db.Debts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        var payment = await db.DebtPayments.SingleOrDefaultAsync(x => x.UserId == userId && x.DebtId == id && x.Id == paymentId);
        if (debt is null || payment is null) return Results.NotFound();
        var balanceBeforeThisPayment = debt.Balance + payment.Amount;
        if (input.Amount > balanceBeforeThisPayment) return FieldError("amount", "A payment cannot exceed the remaining debt balance.");
        debt.Balance = balanceBeforeThisPayment - input.Amount; debt.UpdatedAt = DateTime.UtcNow; payment.Amount = input.Amount; payment.Date = input.Date; payment.Notes = TrimToNull(input.Notes); payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Results.Ok(PaymentView(payment));
    }

    private static async Task<IResult> DeleteDebtPayment(ClaimsPrincipal principal, MoneyBoardDbContext db, Guid id, Guid paymentId)
    {
        var userId = UserId(principal); var debt = await db.Debts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        var payment = await db.DebtPayments.SingleOrDefaultAsync(x => x.UserId == userId && x.DebtId == id && x.Id == paymentId);
        if (debt is null || payment is null) return Results.NotFound();
        debt.Balance = Math.Min(debt.OriginalAmount, debt.Balance + payment.Amount); debt.UpdatedAt = DateTime.UtcNow; db.Remove(payment); await db.SaveChangesAsync(); return Results.NoContent();
    }

    private static BudgetView ToBudgetView(Budget budget, decimal actual) => new(budget.Id, budget.Name, budget.Limit, actual, budget.Limit - actual, budget.Limit == 0 ? 0 : Math.Round(actual / budget.Limit * 100, 1), budget.Year, budget.Month);
    private static SavingsGoalView GoalView(SavingsGoal goal) => new(goal.Id, goal.Name, goal.TargetAmount, goal.CurrentAmount, goal.TargetDate, goal.MonthlyContribution, goal.TargetAmount == 0 ? 0 : Math.Round(goal.CurrentAmount / goal.TargetAmount * 100, 1));
    private static DebtView ToDebtView(Debt debt) => new(debt.Id, debt.Name, debt.OriginalAmount, debt.Balance, debt.InterestRate, debt.MinimumPayment, debt.Frequency, debt.NextDueDate, debt.Status, debt.Payments.OrderByDescending(x => x.Date).Select(PaymentView).ToArray());
    private static DebtPaymentView PaymentView(DebtPayment payment) => new(payment.Id, payment.DebtId, payment.Amount, payment.Date, payment.Notes);
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult FieldError(string field, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    public sealed record TransactionInput(string Type, string Title, decimal Amount, DateOnly Date, string? Category, string? Notes);
    public sealed record TransactionView(Guid Id, string Type, string Title, decimal Amount, DateOnly Date, string Category, string? Notes, DateTime CreatedAt);
    public sealed record BudgetInput(string Name, decimal Limit, int Year, int Month);
    public sealed record BudgetView(Guid Id, string Name, decimal Limit, decimal Actual, decimal Remaining, decimal PercentUsed, int Year, int Month);
    public sealed record SavingsGoalInput(string Name, decimal TargetAmount, decimal CurrentAmount, DateOnly? TargetDate, decimal MonthlyContribution);
    public sealed record SavingsGoalView(Guid Id, string Name, decimal TargetAmount, decimal CurrentAmount, DateOnly? TargetDate, decimal MonthlyContribution, decimal PercentComplete);
    public sealed record DebtInput(string Name, decimal OriginalAmount, decimal Balance, decimal InterestRate, decimal MinimumPayment, string Frequency, DateOnly? NextDueDate, string Status);
    public sealed record DebtPaymentInput(decimal Amount, DateOnly Date, string? Notes);
    public sealed record DebtPaymentView(Guid Id, Guid DebtId, decimal Amount, DateOnly Date, string? Notes);
    public sealed record DebtView(Guid Id, string Name, decimal OriginalAmount, decimal Balance, decimal InterestRate, decimal MinimumPayment, string Frequency, DateOnly? NextDueDate, string Status, DebtPaymentView[] Payments);
    public sealed record TrendView(string Month, decimal Income, decimal Expenses);
    public sealed record DashboardView(int Year, int Month, decimal Balance, decimal Income, decimal Expenses, decimal Saved, decimal SavingsTarget, BudgetView[] Budgets, TrendView[] Trend, TransactionView[] Recent);
}
