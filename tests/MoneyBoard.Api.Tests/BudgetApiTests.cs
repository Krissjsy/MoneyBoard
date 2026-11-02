using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyBoard.Api.Tests;

public sealed class BudgetApiTests
{
    [Fact]
    public async Task Create_and_get_match_expense_totals_case_insensitively_with_month_boundaries_and_excess_spending()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        await AddExpense(client, "Previous month", 5m, new DateOnly(2026, 9, 30), "Food");
        await AddExpense(client, "First day", 10m, new DateOnly(2026, 10, 1), "Food");
        await AddExpense(client, "Last day", 20m, new DateOnly(2026, 10, 31), "FOOD");
        await AddExpense(client, "Following month", 30m, new DateOnly(2026, 11, 1), "food");
        await AddExpense(client, "Unbudgeted category", 7m, new DateOnly(2026, 10, 15), "Garden");

        var create = await client.PostAsJsonAsync("/api/budgets", new { name = "fOoD", limit = 25m, year = 2026, month = 10 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdView = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(30m, createdView.GetProperty("actual").GetDecimal());
        Assert.Equal(-5m, createdView.GetProperty("remaining").GetDecimal());
        Assert.Equal(120m, createdView.GetProperty("percentUsed").GetDecimal());

        var rows = await client.GetFromJsonAsync<JsonElement[]>("/api/budgets?year=2026&month=10");
        var retrievedView = Assert.Single(rows!);
        Assert.Equal(createdView.GetProperty("id").GetGuid(), retrievedView.GetProperty("id").GetGuid());
        Assert.Equal(createdView.GetProperty("actual").GetDecimal(), retrievedView.GetProperty("actual").GetDecimal());
        Assert.Equal(createdView.GetProperty("remaining").GetDecimal(), retrievedView.GetProperty("remaining").GetDecimal());
        Assert.Equal(createdView.GetProperty("percentUsed").GetDecimal(), retrievedView.GetProperty("percentUsed").GetDecimal());

        var duplicate = await client.PostAsJsonAsync("/api/budgets", new { name = "FOOD", limit = 100m, year = 2026, month = 10 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Empty(await client.GetFromJsonAsync<JsonElement[]>("/api/budgets?year=2026&month=9") ?? []);
        Assert.Empty(await client.GetFromJsonAsync<JsonElement[]>("/api/budgets?year=2026&month=11") ?? []);

        var noSpendingBudget = await client.PostAsJsonAsync("/api/budgets", new { name = "Transport", limit = 80m, year = 2026, month = 10 });
        Assert.Equal(HttpStatusCode.Created, noSpendingBudget.StatusCode);
        var noSpendingView = await noSpendingBudget.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, noSpendingView.GetProperty("actual").GetDecimal());
        Assert.Equal(80m, noSpendingView.GetProperty("remaining").GetDecimal());
        Assert.Equal(0m, noSpendingView.GetProperty("percentUsed").GetDecimal());
        // Garden expenses have no matching budget and therefore do not appear as a budget row.
        Assert.DoesNotContain(await client.GetFromJsonAsync<JsonElement[]>("/api/budgets?year=2026&month=10") ?? [], x => x.GetProperty("name").GetString() == "Garden");
    }

    [Fact]
    public async Task Update_uses_the_same_case_insensitive_spending_and_delete_removes_only_the_budget()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        await AddExpense(client, "Trip", 18.25m, new DateOnly(2026, 2, 2), "Travel");
        var created = await client.PostAsJsonAsync("/api/budgets", new { name = "Unused", limit = 50m, year = 2026, month = 2 });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var updated = await client.PutAsJsonAsync($"/api/budgets/{id}", new { name = "tRaVeL", limit = 20m, year = 2026, month = 2 });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedView = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(18.25m, updatedView.GetProperty("actual").GetDecimal());
        Assert.Equal(1.75m, updatedView.GetProperty("remaining").GetDecimal());
        var retrieved = Assert.Single(await client.GetFromJsonAsync<JsonElement[]>("/api/budgets?year=2026&month=2") ?? []);
        Assert.Equal(18.25m, retrieved.GetProperty("actual").GetDecimal());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/budgets/{id}")).StatusCode);
        Assert.Empty(await client.GetFromJsonAsync<JsonElement[]>("/api/budgets?year=2026&month=2") ?? []);
        var transactions = await client.GetFromJsonAsync<JsonElement[]>("/api/transactions");
        Assert.Single(transactions!);
    }

    [Fact]
    public async Task Rejects_invalid_budget_fields_without_creating_budgets()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        var invalid = new object[]
        {
            new { name = " ", limit = 10m, year = 2026, month = 10 },
            new { name = "Food", limit = 0m, year = 2026, month = 10 },
            new { name = "Food", limit = -1m, year = 2026, month = 10 },
            new { name = "Food", limit = 1.234m, year = 2026, month = 10 },
            new { name = "Food", limit = 10m, year = 2026, month = 13 },
            new { name = "Food", limit = 10m, year = 1999, month = 10 },
            new { name = new string('x', 61), limit = 10m, year = 2026, month = 10 },
        };

        foreach (var body in invalid)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/budgets", body)).StatusCode);
        Assert.Empty(await client.GetFromJsonAsync<JsonElement[]>("/api/budgets?year=2026&month=10") ?? []);
    }

    private static async Task AddExpense(HttpClient client, string title, decimal amount, DateOnly date, string category)
    {
        var response = await client.PostAsJsonAsync("/api/transactions", new { type = "expense", title, amount, date, category, notes = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<HttpClient> NewAuthenticatedClient(SetupApiFactory factory)
    {
        var client = factory.CreateClient();
        await factory.EnsureDatabaseAsync();
        var email = $"budgets-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { name = "Budget tests", email, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
