using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyBoard.Api.Tests;

public sealed class DashboardApiTests
{
    [Fact]
    public async Task Dashboard_uses_selected_month_boundaries_and_keeps_all_time_net_and_goal_totals_separate()
    {
        using var factory = new SetupApiFactory(useInMemory: true);
        var client = await NewAuthenticatedClient(factory);

        await AddIncome(client, "Previous month", 100m, new DateOnly(2026, 9, 30));
        await AddIncome(client, "First day income", 100m, new DateOnly(2026, 10, 1));
        await AddIncome(client, "Last day income", 250m, new DateOnly(2026, 10, 31));
        await AddIncome(client, "Following month", 500m, new DateOnly(2026, 11, 1));

        await AddExpense(client, "Previous month", 11m, new DateOnly(2026, 9, 30), "Unbudgeted");
        await AddExpense(client, "First day expense", 13m, new DateOnly(2026, 10, 1), "Unbudgeted");
        await AddExpense(client, "Last day expense", 17m, new DateOnly(2026, 10, 31), "Other unbudgeted");
        await AddExpense(client, "Following month", 19m, new DateOnly(2026, 11, 1), "Unbudgeted");

        await AddGoal(client, "Emergency fund", 125m);
        await AddGoal(client, "Holiday fund", 75m);

        var response = await client.GetAsync("/api/dashboard?year=2026&month=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2026, dashboard.GetProperty("year").GetInt32());
        Assert.Equal(10, dashboard.GetProperty("month").GetInt32());
        Assert.Equal(350m, dashboard.GetProperty("income").GetDecimal());
        // Both expenses are in categories without budgets and are still included in Money Out.
        Assert.Equal(30m, dashboard.GetProperty("expenses").GetDecimal());
        Assert.Equal(890m, dashboard.GetProperty("balance").GetDecimal());
        Assert.Equal(200m, dashboard.GetProperty("saved").GetDecimal());
        Assert.Equal(2000m, dashboard.GetProperty("savingsTarget").GetDecimal());
    }

    private static async Task AddIncome(HttpClient client, string source, decimal amount, DateOnly date)
    {
        var response = await client.PostAsJsonAsync("/api/transactions", new { type = "income", title = source, amount, date, category = (string?)null, notes = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AddExpense(HttpClient client, string title, decimal amount, DateOnly date, string category)
    {
        var response = await client.PostAsJsonAsync("/api/transactions", new { type = "expense", title, amount, date, category, notes = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AddGoal(HttpClient client, string name, decimal currentAmount)
    {
        var response = await client.PostAsJsonAsync("/api/savings-goals", new { name, targetAmount = 1000m, currentAmount, targetDate = (DateOnly?)null, monthlyContribution = 25m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<HttpClient> NewAuthenticatedClient(SetupApiFactory factory)
    {
        var client = factory.CreateClient();
        await factory.EnsureDatabaseAsync();
        var email = $"dashboard-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { name = "Dashboard tests", email, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
