using Microsoft.AspNetCore.Identity;

namespace MoneyBoard.Api.Models;

public sealed class MoneyBoardUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public string Currency { get; set; } = "GBP";
    public DateTime CreatedAt { get; set; }
    public ICollection<Income> Incomes { get; set; } = [];
    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
    public ICollection<Debt> Debts { get; set; } = [];
    public ICollection<SavingsGoal> SavingsGoals { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}

public abstract class OwnedRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class Income : OwnedRecord { public string Source { get; set; } = ""; public decimal Amount { get; set; } public DateOnly Date { get; set; } public string? Notes { get; set; } public bool IsRecurring { get; set; } public string? Recurrence { get; set; } }
public sealed class ExpenseCategory : OwnedRecord { public string Name { get; set; } = ""; public string? Color { get; set; } public bool IsArchived { get; set; } public ICollection<Expense> Expenses { get; set; } = []; }
public sealed class Expense : OwnedRecord { public Guid CategoryId { get; set; } public ExpenseCategory? Category { get; set; } public string Description { get; set; } = ""; public decimal Amount { get; set; } public DateOnly Date { get; set; } public string? Notes { get; set; } public bool IsRecurring { get; set; } public string? Recurrence { get; set; } }
public sealed class Budget : OwnedRecord { public string Name { get; set; } = ""; public decimal Limit { get; set; } public int Year { get; set; } public int Month { get; set; } }
public sealed class Debt : OwnedRecord { public string Name { get; set; } = ""; public decimal OriginalAmount { get; set; } public decimal Balance { get; set; } public decimal InterestRate { get; set; } public decimal MinimumPayment { get; set; } public string Frequency { get; set; } = "Monthly"; public DateOnly? NextDueDate { get; set; } public string Status { get; set; } = "Active"; public ICollection<DebtPayment> Payments { get; set; } = []; }
public sealed class DebtPayment : OwnedRecord { public Guid DebtId { get; set; } public Debt? Debt { get; set; } public decimal Amount { get; set; } public DateOnly Date { get; set; } public string? Notes { get; set; } }
public sealed class SavingsGoal : OwnedRecord { public string Name { get; set; } = ""; public decimal TargetAmount { get; set; } public decimal CurrentAmount { get; set; } public DateOnly? TargetDate { get; set; } public decimal MonthlyContribution { get; set; } public ICollection<SavingsContribution> Contributions { get; set; } = []; }
public sealed class SavingsContribution : OwnedRecord { public Guid SavingsGoalId { get; set; } public SavingsGoal? SavingsGoal { get; set; } public decimal Amount { get; set; } public DateOnly Date { get; set; } public string? Notes { get; set; } }
public sealed class Notification : OwnedRecord { public string Title { get; set; } = ""; public string Message { get; set; } = ""; public string Type { get; set; } = "Reminder"; public DateTime? ReadAt { get; set; } }
