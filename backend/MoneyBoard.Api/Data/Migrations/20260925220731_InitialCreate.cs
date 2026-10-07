// Equivalent reconstruction of migration 20260925220731_InitialCreate.
// Reconstructed from backend/Database/001_InitialCreate.sql and the EF model;
// this is not the recovered original migration source.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBoard.Api.Data.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AspNetRoles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AspNetRoles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "GBP"),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                AccessFailedCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AspNetUsers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AspNetRoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                table.ForeignKey("FK_AspNetRoleClaims_AspNetRoles_RoleId", x => x.RoleId, "AspNetRoles", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey("FK_AspNetUserClaims_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey("FK_AspNetUserLogins_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserRoles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey("FK_AspNetUserRoles_AspNetRoles_RoleId", x => x.RoleId, "AspNetRoles", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_AspNetUserRoles_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LoginProvider = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey("FK_AspNetUserTokens_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Budgets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Limit = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Month = table.Column<int>(type: "int", nullable: false),
                MoneyBoardUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Budgets", x => x.Id);
                table.ForeignKey("FK_Budgets_AspNetUsers_MoneyBoardUserId", x => x.MoneyBoardUserId, "AspNetUsers", "Id");
                table.ForeignKey("FK_Budgets_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Debts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OriginalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Balance = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                InterestRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                MinimumPayment = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                NextDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                MoneyBoardUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Debts", x => x.Id);
                table.ForeignKey("FK_Debts_AspNetUsers_MoneyBoardUserId", x => x.MoneyBoardUserId, "AspNetUsers", "Id");
                table.ForeignKey("FK_Debts_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ExpenseCategories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsArchived = table.Column<bool>(type: "bit", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                table.ForeignKey("FK_ExpenseCategories_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Incomes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                Recurrence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                MoneyBoardUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Incomes", x => x.Id);
                table.ForeignKey("FK_Incomes_AspNetUsers_MoneyBoardUserId", x => x.MoneyBoardUserId, "AspNetUsers", "Id");
                table.ForeignKey("FK_Incomes_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ReadAt = table.Column<DateTime?>(type: "datetime2", nullable: true),
                MoneyBoardUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
                table.ForeignKey("FK_Notifications_AspNetUsers_MoneyBoardUserId", x => x.MoneyBoardUserId, "AspNetUsers", "Id");
                table.ForeignKey("FK_Notifications_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SavingsGoals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                TargetAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                CurrentAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                MonthlyContribution = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                MoneyBoardUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavingsGoals", x => x.Id);
                table.ForeignKey("FK_SavingsGoals_AspNetUsers_MoneyBoardUserId", x => x.MoneyBoardUserId, "AspNetUsers", "Id");
                table.ForeignKey("FK_SavingsGoals_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DebtPayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DebtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DebtPayments", x => x.Id);
                table.ForeignKey("FK_DebtPayments_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_DebtPayments_Debts_DebtId", x => x.DebtId, "Debts", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Expenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Description = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                Recurrence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                MoneyBoardUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Expenses", x => x.Id);
                table.ForeignKey("FK_Expenses_AspNetUsers_MoneyBoardUserId", x => x.MoneyBoardUserId, "AspNetUsers", "Id");
                table.ForeignKey("FK_Expenses_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_Expenses_ExpenseCategories_CategoryId", x => x.CategoryId, "ExpenseCategories", "Id", onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateTable(
            name: "SavingsContributions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SavingsGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavingsContributions", x => x.Id);
                table.ForeignKey("FK_SavingsContributions_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_SavingsContributions_SavingsGoals_SavingsGoalId", x => x.SavingsGoalId, "SavingsGoals", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_AspNetRoleClaims_RoleId", "AspNetRoleClaims", "RoleId");
        migrationBuilder.CreateIndex("RoleNameIndex", "AspNetRoles", "NormalizedName", unique: true, filter: "[NormalizedName] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_AspNetUserClaims_UserId", "AspNetUserClaims", "UserId");
        migrationBuilder.CreateIndex("IX_AspNetUserLogins_UserId", "AspNetUserLogins", "UserId");
        migrationBuilder.CreateIndex("IX_AspNetUserRoles_RoleId", "AspNetUserRoles", "RoleId");
        migrationBuilder.CreateIndex("EmailIndex", "AspNetUsers", "NormalizedEmail");
        migrationBuilder.CreateIndex("UserNameIndex", "AspNetUsers", "NormalizedUserName", unique: true, filter: "[NormalizedUserName] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_Budgets_MoneyBoardUserId", "Budgets", "MoneyBoardUserId");
        migrationBuilder.CreateIndex("IX_Budgets_UserId", "Budgets", "UserId");
        migrationBuilder.CreateIndex("IX_Budgets_UserId_Year_Month_Name", "Budgets", new[] { "UserId", "Year", "Month", "Name" }, unique: true);
        migrationBuilder.CreateIndex("IX_DebtPayments_DebtId", "DebtPayments", "DebtId");
        migrationBuilder.CreateIndex("IX_DebtPayments_UserId", "DebtPayments", "UserId");
        migrationBuilder.CreateIndex("IX_Debts_MoneyBoardUserId", "Debts", "MoneyBoardUserId");
        migrationBuilder.CreateIndex("IX_Debts_UserId", "Debts", "UserId");
        migrationBuilder.CreateIndex("IX_ExpenseCategories_UserId", "ExpenseCategories", "UserId");
        migrationBuilder.CreateIndex("IX_Expenses_CategoryId", "Expenses", "CategoryId");
        migrationBuilder.CreateIndex("IX_Expenses_MoneyBoardUserId", "Expenses", "MoneyBoardUserId");
        migrationBuilder.CreateIndex("IX_Expenses_UserId", "Expenses", "UserId");
        migrationBuilder.CreateIndex("IX_Incomes_MoneyBoardUserId", "Incomes", "MoneyBoardUserId");
        migrationBuilder.CreateIndex("IX_Incomes_UserId", "Incomes", "UserId");
        migrationBuilder.CreateIndex("IX_Notifications_MoneyBoardUserId", "Notifications", "MoneyBoardUserId");
        migrationBuilder.CreateIndex("IX_Notifications_UserId", "Notifications", "UserId");
        migrationBuilder.CreateIndex("IX_SavingsContributions_SavingsGoalId", "SavingsContributions", "SavingsGoalId");
        migrationBuilder.CreateIndex("IX_SavingsContributions_UserId", "SavingsContributions", "UserId");
        migrationBuilder.CreateIndex("IX_SavingsGoals_MoneyBoardUserId", "SavingsGoals", "MoneyBoardUserId");
        migrationBuilder.CreateIndex("IX_SavingsGoals_UserId", "SavingsGoals", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AspNetRoleClaims");
        migrationBuilder.DropTable(name: "AspNetUserClaims");
        migrationBuilder.DropTable(name: "AspNetUserLogins");
        migrationBuilder.DropTable(name: "AspNetUserRoles");
        migrationBuilder.DropTable(name: "AspNetUserTokens");
        migrationBuilder.DropTable(name: "DebtPayments");
        migrationBuilder.DropTable(name: "Expenses");
        migrationBuilder.DropTable(name: "SavingsContributions");
        migrationBuilder.DropTable(name: "Budgets");
        migrationBuilder.DropTable(name: "Debts");
        migrationBuilder.DropTable(name: "ExpenseCategories");
        migrationBuilder.DropTable(name: "Incomes");
        migrationBuilder.DropTable(name: "Notifications");
        migrationBuilder.DropTable(name: "SavingsGoals");
        migrationBuilder.DropTable(name: "AspNetRoles");
        migrationBuilder.DropTable(name: "AspNetUsers");
    }
}
