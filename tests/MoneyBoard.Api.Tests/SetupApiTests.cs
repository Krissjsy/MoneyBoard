using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoneyBoard.Api.Data;
using Xunit;

namespace MoneyBoard.Api.Tests;

public sealed class SetupApiTests
{
    private static readonly object CompleteBody = new
    {
        currency = "GBP",
        year = 2026,
        month = 10,
        incomes = new[] { new { name = "Salary", amount = 4200m, frequency = "Monthly" } },
        expenses = new[] { new { name = "Housing", amount = 1500m }, new { name = "Food", amount = 400m } },
        goals = new[] { new { name = "Emergency fund", targetAmount = 5000m, monthlyContribution = 300m, targetDate = (DateOnly?)new DateOnly(2027, 10, 1) } }
    };

    [Fact]
    public async Task Authenticated_user_can_save_progress_and_resume_after_a_new_login()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        var draft = new { currency = "GBP", incomes = new[] { new { name = "Salary", amount = "4200", frequency = "Monthly" } }, expenses = Array.Empty<object>(), goals = Array.Empty<object>() };
        var save = await client.PutAsJsonAsync("/api/setup/progress", new { step = 3, data = draft });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.True((await save.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("saved").GetBoolean());

        var refreshedClient = await LoginAgain(client);
        var profile = await refreshedClient.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.False(profile.GetProperty("setupCompleted").GetBoolean());
        Assert.NotEqual("null", profile.GetProperty("setupProgress").GetRawText());
        var progress = JsonDocument.Parse(profile.GetProperty("setupProgress").GetString()!);
        Assert.Equal(3, progress.RootElement.GetProperty("step").GetInt32());
    }

    [Fact]
    public async Task Registration_trims_and_normalizes_email_rejects_duplicates_and_login_uses_the_same_normalization()
    {
        using var factory = new SetupApiFactory();
        var client = factory.CreateClient();
        await factory.EnsureDatabaseAsync();
        var email = $"auth-{Guid.NewGuid():N}@example.test";

        var created = await client.PostAsJsonAsync("/api/auth/register", new { name = "Auth test", email = $"  {email}  ", password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { name = "Duplicate", email = $" {email.ToUpperInvariant()} ", password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("already registered", (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);

        await SignIn(client, $" {email.ToUpperInvariant()} ");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == email.ToUpperInvariant()));
        var emailIndex = db.Model.FindEntityType(typeof(MoneyBoard.Api.Models.MoneyBoardUser))!.GetIndexes().Single(x => x.GetDatabaseName() == "EmailIndex");
        Assert.True(emailIndex.IsUnique);
        Assert.Equal("[NormalizedEmail] IS NOT NULL", emailIndex.GetFilter());
    }

    [Fact]
    public async Task Completion_persists_estimates_budgets_goals_and_status_without_transactions_and_is_idempotent()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);

        var first = await client.PostAsJsonAsync("/api/setup/complete", CompleteBody);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var repeated = await client.PostAsJsonAsync("/api/setup/complete", CompleteBody);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        var returningClient = await LoginAgain(client);
        var profile = await returningClient.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.True(profile!.GetProperty("setupCompleted").GetBoolean());
        Assert.Null(profile.GetProperty("setupProgress").GetString());
        Assert.Contains("Salary", profile.GetProperty("setupData").GetString());
        var userId = await GetUserId(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
            var budgets = await db.Budgets.Where(x => x.UserId == userId && x.Year == 2026 && x.Month == 10).ToListAsync();
            Assert.Equal(2, budgets.Count);
            Assert.Contains(budgets, x => x.Name == "Housing" && x.Limit == 1500m);
            Assert.Contains(budgets, x => x.Name == "Food" && x.Limit == 400m);
        }
        var goals = await returningClient.GetFromJsonAsync<JsonElement[]>("/api/savings-goals");
        Assert.Single(goals!);
        Assert.Equal("Emergency fund", goals![0].GetProperty("name").GetString());
        var transactions = await returningClient.GetFromJsonAsync<JsonElement[]>("/api/transactions");
        Assert.Empty(transactions!);
    }

    [Fact]
    public async Task Completion_preserves_existing_matching_budgets_and_goals()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        var userId = await GetUserId(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
            db.Budgets.Add(new MoneyBoard.Api.Models.Budget { UserId = userId, Name = "Housing", Limit = 1777m, Year = 2026, Month = 10 });
            db.SavingsGoals.Add(new MoneyBoard.Api.Models.SavingsGoal { UserId = userId, Name = "Emergency fund", TargetAmount = 9000m, CurrentAmount = 125m, MonthlyContribution = 450m });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/setup/complete", CompleteBody)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
            var budgets = await db.Budgets.Where(x => x.UserId == userId && x.Name == "Housing").ToListAsync();
            Assert.Single(budgets);
            Assert.Equal(1777m, budgets[0].Limit);
            var goals = await db.SavingsGoals.Where(x => x.UserId == userId && x.Name == "Emergency fund").ToListAsync();
            Assert.Single(goals);
            Assert.Equal(9000m, goals[0].TargetAmount);
            Assert.Equal(125m, goals[0].CurrentAmount);
            Assert.Equal(450m, goals[0].MonthlyContribution);
        }
    }

    [Fact]
    public async Task Skip_is_not_a_completion_and_invalid_inputs_do_not_persist()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        var progress = await client.PutAsJsonAsync("/api/setup/progress", new { step = 2, data = new { incomes = Array.Empty<object>() } });
        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
        var invalid = await client.PostAsJsonAsync("/api/setup/complete", new
        {
            currency = "GBP", year = 2026, month = 10,
            incomes = new[] { new { name = "Salary", amount = -1m, frequency = "Monthly" } },
            expenses = Array.Empty<object>(), goals = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var invalidExpense = await client.PostAsJsonAsync("/api/setup/complete", new
        {
            currency = "GBP", year = 2026, month = 10, incomes = Array.Empty<object>(),
            expenses = new[] { new { name = "Housing", amount = -1m } }, goals = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidExpense.StatusCode);
        var invalidGoal = await client.PostAsJsonAsync("/api/setup/complete", new
        {
            currency = "GBP", year = 2026, month = 10, incomes = Array.Empty<object>(), expenses = Array.Empty<object>(),
            goals = new[] { new { name = "Emergency fund", targetAmount = -1m, monthlyContribution = 0m, targetDate = (DateOnly?)null } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidGoal.StatusCode);
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.False(profile.GetProperty("setupCompleted").GetBoolean());
        Assert.NotNull(profile.GetProperty("setupProgress").GetString());
        Assert.Equal(0, await CountBudgets(factory, await GetUserId(client)));
    }

    [Fact]
    public async Task Unauthenticated_setup_calls_are_rejected()
    {
        using var factory = new SetupApiFactory();
        var client = factory.CreateClient();
        await factory.EnsureDatabaseAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/setup/progress", new { step = 1, data = new { } })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/setup/complete", CompleteBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Users_cannot_read_another_users_setup_or_budget_records()
    {
        using var factory = new SetupApiFactory();
        var first = await NewAuthenticatedClient(factory, "first");
        var firstComplete = await first.PostAsJsonAsync("/api/setup/complete", CompleteBody);
        Assert.Equal(HttpStatusCode.OK, firstComplete.StatusCode);
        var second = await NewAuthenticatedClient(factory, "second");

        var secondProfile = await second.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.False(secondProfile.GetProperty("setupCompleted").GetBoolean());
        Assert.Null(secondProfile.GetProperty("setupData").GetString());
        Assert.Equal(0, await CountBudgets(factory, await GetUserId(second)));
        Assert.Empty(await second.GetFromJsonAsync<JsonElement[]>("/api/savings-goals") ?? []);
    }

    [Fact]
    public async Task Persistence_failure_rolls_back_and_does_not_mark_setup_complete()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        factory.SaveChangesInterceptor.FailNextSave();

        var failed = await client.PostAsJsonAsync("/api/setup/complete", CompleteBody);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.False(profile.GetProperty("setupCompleted").GetBoolean());
        Assert.Null(profile.GetProperty("setupData").GetString());
        Assert.Equal(0, await CountBudgets(factory, await GetUserId(client)));
        Assert.Empty(await client.GetFromJsonAsync<JsonElement[]>("/api/savings-goals") ?? []);
    }

    private static async Task<HttpClient> NewAuthenticatedClient(SetupApiFactory factory, string label = "user")
    {
        var client = factory.CreateClient();
        await factory.EnsureDatabaseAsync();
        var email = $"{label}-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { name = label, email, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        return await SignIn(client, email);
    }

    private static async Task<Guid> GetUserId(HttpClient client)
    {
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        return profile.GetProperty("id").GetGuid();
    }

    private static async Task<int> CountBudgets(SetupApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
        return await db.Budgets.CountAsync(x => x.UserId == userId);
    }

    private static async Task<HttpClient> LoginAgain(HttpClient client)
    {
        var email = "";
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        email = me.GetProperty("email").GetString()!;
        client.DefaultRequestHeaders.Authorization = null;
        return await SignIn(client, email);
    }

    private static async Task<HttpClient> SignIn(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var result = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.GetProperty("accessToken").GetString());
        return client;
    }
}
