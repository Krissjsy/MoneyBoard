IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AspNetRoles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetUsers] (
    [Id] uniqueidentifier NOT NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [Currency] nvarchar(3) NOT NULL DEFAULT N'GBP',
    [CreatedAt] datetime2 NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Budgets] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Limit] decimal(12,2) NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [MoneyBoardUserId] uniqueidentifier NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Budgets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Budgets_AspNetUsers_MoneyBoardUserId] FOREIGN KEY ([MoneyBoardUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Budgets_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Debts] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [OriginalAmount] decimal(12,2) NOT NULL,
    [Balance] decimal(12,2) NOT NULL,
    [InterestRate] decimal(6,3) NOT NULL,
    [MinimumPayment] decimal(12,2) NOT NULL,
    [Frequency] nvarchar(max) NOT NULL,
    [NextDueDate] date NULL,
    [Status] nvarchar(max) NOT NULL,
    [MoneyBoardUserId] uniqueidentifier NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Debts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Debts_AspNetUsers_MoneyBoardUserId] FOREIGN KEY ([MoneyBoardUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Debts_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ExpenseCategories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(60) NOT NULL,
    [Color] nvarchar(max) NULL,
    [IsArchived] bit NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ExpenseCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExpenseCategories_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Incomes] (
    [Id] uniqueidentifier NOT NULL,
    [Source] nvarchar(100) NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Date] date NOT NULL,
    [Notes] nvarchar(max) NULL,
    [IsRecurring] bit NOT NULL,
    [Recurrence] nvarchar(max) NULL,
    [MoneyBoardUserId] uniqueidentifier NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Incomes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Incomes_AspNetUsers_MoneyBoardUserId] FOREIGN KEY ([MoneyBoardUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Incomes_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Notifications] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(160) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Type] nvarchar(40) NOT NULL,
    [ReadAt] datetime2 NULL,
    [MoneyBoardUserId] uniqueidentifier NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notifications_AspNetUsers_MoneyBoardUserId] FOREIGN KEY ([MoneyBoardUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SavingsGoals] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [TargetAmount] decimal(12,2) NOT NULL,
    [CurrentAmount] decimal(12,2) NOT NULL,
    [TargetDate] date NULL,
    [MonthlyContribution] decimal(12,2) NOT NULL,
    [MoneyBoardUserId] uniqueidentifier NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SavingsGoals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SavingsGoals_AspNetUsers_MoneyBoardUserId] FOREIGN KEY ([MoneyBoardUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_SavingsGoals_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [DebtPayments] (
    [Id] uniqueidentifier NOT NULL,
    [DebtId] uniqueidentifier NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Date] date NOT NULL,
    [Notes] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DebtPayments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DebtPayments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DebtPayments_Debts_DebtId] FOREIGN KEY ([DebtId]) REFERENCES [Debts] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Expenses] (
    [Id] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [Description] nvarchar(160) NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Date] date NOT NULL,
    [Notes] nvarchar(max) NULL,
    [IsRecurring] bit NOT NULL,
    [Recurrence] nvarchar(max) NULL,
    [MoneyBoardUserId] uniqueidentifier NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Expenses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Expenses_AspNetUsers_MoneyBoardUserId] FOREIGN KEY ([MoneyBoardUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Expenses_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Expenses_ExpenseCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ExpenseCategories] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SavingsContributions] (
    [Id] uniqueidentifier NOT NULL,
    [SavingsGoalId] uniqueidentifier NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Date] date NOT NULL,
    [Notes] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SavingsContributions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SavingsContributions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SavingsContributions_SavingsGoals_SavingsGoalId] FOREIGN KEY ([SavingsGoalId]) REFERENCES [SavingsGoals] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_Budgets_MoneyBoardUserId] ON [Budgets] ([MoneyBoardUserId]);
GO

CREATE INDEX [IX_Budgets_UserId] ON [Budgets] ([UserId]);
GO

CREATE UNIQUE INDEX [IX_Budgets_UserId_Year_Month_Name] ON [Budgets] ([UserId], [Year], [Month], [Name]);
GO

CREATE INDEX [IX_DebtPayments_DebtId] ON [DebtPayments] ([DebtId]);
GO

CREATE INDEX [IX_DebtPayments_UserId] ON [DebtPayments] ([UserId]);
GO

CREATE INDEX [IX_Debts_MoneyBoardUserId] ON [Debts] ([MoneyBoardUserId]);
GO

CREATE INDEX [IX_Debts_UserId] ON [Debts] ([UserId]);
GO

CREATE INDEX [IX_ExpenseCategories_UserId] ON [ExpenseCategories] ([UserId]);
GO

CREATE INDEX [IX_Expenses_CategoryId] ON [Expenses] ([CategoryId]);
GO

CREATE INDEX [IX_Expenses_MoneyBoardUserId] ON [Expenses] ([MoneyBoardUserId]);
GO

CREATE INDEX [IX_Expenses_UserId] ON [Expenses] ([UserId]);
GO

CREATE INDEX [IX_Incomes_MoneyBoardUserId] ON [Incomes] ([MoneyBoardUserId]);
GO

CREATE INDEX [IX_Incomes_UserId] ON [Incomes] ([UserId]);
GO

CREATE INDEX [IX_Notifications_MoneyBoardUserId] ON [Notifications] ([MoneyBoardUserId]);
GO

CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
GO

CREATE INDEX [IX_SavingsContributions_SavingsGoalId] ON [SavingsContributions] ([SavingsGoalId]);
GO

CREATE INDEX [IX_SavingsContributions_UserId] ON [SavingsContributions] ([UserId]);
GO

CREATE INDEX [IX_SavingsGoals_MoneyBoardUserId] ON [SavingsGoals] ([MoneyBoardUserId]);
GO

CREATE INDEX [IX_SavingsGoals_UserId] ON [SavingsGoals] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260925220731_InitialCreate', N'8.0.11');
GO

COMMIT;
GO

