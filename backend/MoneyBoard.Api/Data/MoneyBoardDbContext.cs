using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MoneyBoard.Api.Models;

namespace MoneyBoard.Api.Data;

public sealed class MoneyBoardDbContext(DbContextOptions<MoneyBoardDbContext> options) : IdentityDbContext<MoneyBoardUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();
    public DbSet<SavingsContribution> SavingsContributions => Set<SavingsContribution>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<MoneyBoardUser>().Property(x => x.DisplayName).HasMaxLength(100);
        b.Entity<MoneyBoardUser>().Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("GBP");
        b.Entity<Income>().Property(x => x.Amount).HasPrecision(12, 2); b.Entity<Income>().Property(x => x.Source).HasMaxLength(100);
        b.Entity<Expense>().Property(x => x.Amount).HasPrecision(12, 2); b.Entity<Expense>().Property(x => x.Description).HasMaxLength(160);
        b.Entity<ExpenseCategory>().Property(x => x.Name).HasMaxLength(60);
        b.Entity<Budget>().Property(x => x.Limit).HasPrecision(12, 2); b.Entity<Budget>().HasIndex(x => new { x.UserId, x.Year, x.Month, x.Name }).IsUnique();
        b.Entity<Debt>().Property(x => x.OriginalAmount).HasPrecision(12, 2); b.Entity<Debt>().Property(x => x.Balance).HasPrecision(12, 2); b.Entity<Debt>().Property(x => x.InterestRate).HasPrecision(6, 3); b.Entity<Debt>().Property(x => x.MinimumPayment).HasPrecision(12, 2);
        b.Entity<DebtPayment>().Property(x => x.Amount).HasPrecision(12, 2); b.Entity<Debt>().HasMany(x => x.Payments).WithOne(x => x.Debt).HasForeignKey(x => x.DebtId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SavingsGoal>().Property(x => x.TargetAmount).HasPrecision(12, 2); b.Entity<SavingsGoal>().Property(x => x.CurrentAmount).HasPrecision(12, 2); b.Entity<SavingsGoal>().Property(x => x.MonthlyContribution).HasPrecision(12, 2);
        b.Entity<SavingsContribution>().Property(x => x.Amount).HasPrecision(12, 2); b.Entity<SavingsGoal>().HasMany(x => x.Contributions).WithOne(x => x.SavingsGoal).HasForeignKey(x => x.SavingsGoalId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Notification>().Property(x => x.Title).HasMaxLength(160); b.Entity<Notification>().Property(x => x.Type).HasMaxLength(40);
        foreach (var type in new[] { typeof(Income), typeof(Expense), typeof(ExpenseCategory), typeof(Budget), typeof(Debt), typeof(DebtPayment), typeof(SavingsGoal), typeof(SavingsContribution), typeof(Notification) })
        {
            var entity = b.Entity(type); entity.HasIndex(nameof(OwnedRecord.UserId)); entity.HasOne(typeof(MoneyBoardUser)).WithMany().HasForeignKey(nameof(OwnedRecord.UserId)).OnDelete(DeleteBehavior.Cascade);
        }
        b.Entity<DebtPayment>().HasOne<MoneyBoardUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<SavingsContribution>().HasOne<MoneyBoardUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Expense>().HasOne(x => x.Category).WithMany(x => x.Expenses).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
