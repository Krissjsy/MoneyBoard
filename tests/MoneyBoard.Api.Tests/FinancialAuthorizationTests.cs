using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyBoard.Api.Tests;

public sealed class FinancialAuthorizationTests
{
    [Fact]
    public async Task Users_cannot_read_or_change_another_users_financial_records()
    {
        // SQLite cannot translate the dashboard's decimal SumAsync aggregates;
        // this test targets ownership filters, so use the isolated in-memory provider.
        using var factory = new SetupApiFactory(useInMemory: true);
        await factory.EnsureDatabaseAsync();
        using var userA = await NewAuthenticatedClient(factory, "owner");
        using var userB = await NewAuthenticatedClient(factory, "other");
        using var anonymous = factory.CreateClient();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = today.Year;
        var month = today.Month;

        var incomeId = await Create(userA, "/api/transactions", new
        {
            type = "income", title = "Owner income", amount = 2500m, date = today, category = (string?)null, notes = "Owner only"
        });
        var expenseId = await Create(userA, "/api/transactions", new
        {
            type = "expense", title = "Owner expense", amount = 12.34m, date = today, category = "Owner category", notes = "Owner only"
        });
        var budgetId = await Create(userA, "/api/budgets", new { name = "Owner budget", limit = 500m, year, month });
        var goalId = await Create(userA, "/api/savings-goals", new
        {
            name = "Owner goal", targetAmount = 2000m, currentAmount = 100m, targetDate = (DateOnly?)today.AddMonths(6), monthlyContribution = 50m
        });
        var debtId = await Create(userA, "/api/debts", new
        {
            name = "Owner debt", originalAmount = 1000m, balance = 1000m, interestRate = 2.5m,
            minimumPayment = 25m, frequency = "Monthly", nextDueDate = (DateOnly?)today.AddDays(30), status = "Active"
        });
        var secondDebtId = await Create(userA, "/api/debts", new
        {
            name = "Owner second debt", originalAmount = 500m, balance = 500m, interestRate = 0m,
            minimumPayment = 10m, frequency = "Monthly", nextDueDate = (DateOnly?)today.AddDays(30), status = "Active"
        });
        var paymentId = await Create(userA, $"/api/debts/{debtId}/payments", new { amount = 100m, date = today, notes = "Owner payment" });
        var userBDebtId = await Create(userB, "/api/debts", new
        {
            name = "Other user's debt", originalAmount = 600m, balance = 600m, interestRate = 0m,
            minimumPayment = 10m, frequency = "Monthly", nextDueDate = (DateOnly?)today.AddDays(30), status = "Active"
        });
        var userBSecondDebtId = await Create(userB, "/api/debts", new
        {
            name = "Other user's second debt", originalAmount = 400m, balance = 400m, interestRate = 0m,
            minimumPayment = 10m, frequency = "Monthly", nextDueDate = (DateOnly?)today.AddDays(30), status = "Active"
        });
        var userBPaymentId = await Create(userB, $"/api/debts/{userBDebtId}/payments", new { amount = 25m, date = today, notes = "Other user's payment" });

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/budgets?year={year}&month={month}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/savings-goals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/debts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/dashboard?year={year}&month={month}")).StatusCode);
        await AssertStatus(anonymous, HttpMethod.Post, "/api/transactions", HttpStatusCode.Unauthorized,
            new { type = "income", title = "Anonymous", amount = 10m, date = today, category = (string?)null, notes = (string?)null });
        await AssertStatus(anonymous, HttpMethod.Post, "/api/budgets", HttpStatusCode.Unauthorized, new { name = "Anonymous", limit = 10m, year, month });
        await AssertStatus(anonymous, HttpMethod.Post, "/api/savings-goals", HttpStatusCode.Unauthorized,
            new { name = "Anonymous", targetAmount = 10m, currentAmount = 0m, targetDate = (DateOnly?)today.AddMonths(1), monthlyContribution = 0m });
        await AssertStatus(anonymous, HttpMethod.Post, "/api/debts", HttpStatusCode.Unauthorized,
            new { name = "Anonymous", originalAmount = 10m, balance = 10m, interestRate = 0m, minimumPayment = 0m, frequency = "Monthly", nextDueDate = (DateOnly?)today.AddDays(30), status = "Active" });
        await AssertStatus(anonymous, HttpMethod.Post, $"/api/debts/{Guid.NewGuid()}/payments", HttpStatusCode.Unauthorized,
            new { amount = 1m, date = today, notes = (string?)null });

        // The API has collection GET routes, but no item GET route for these IDs.
        // Assert the method contract (405) instead of treating it as an ownership check.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await userB.GetAsync($"/api/transactions/income/{incomeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await userB.GetAsync($"/api/transactions/expense/{expenseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await userB.GetAsync($"/api/budgets/{budgetId}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await userB.GetAsync($"/api/savings-goals/{goalId}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await userB.GetAsync($"/api/debts/{debtId}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await userB.GetAsync($"/api/debts/{debtId}/payments/{paymentId}")).StatusCode);

        Assert.Empty(await ReadArray(userB, "/api/transactions"));
        Assert.Empty(await ReadArray(userB, $"/api/budgets?year={year}&month={month}"));
        Assert.Empty(await ReadArray(userB, "/api/savings-goals"));
        var userBDebts = await ReadArray(userB, "/api/debts");
        Assert.Equal(2, userBDebts.Length);
        Assert.Contains(userBDebts, row => row.GetProperty("id").GetGuid() == userBDebtId);
        Assert.Contains(userBDebts, row => row.GetProperty("id").GetGuid() == userBSecondDebtId);
        Assert.DoesNotContain(userBDebts, row => row.GetProperty("id").GetGuid() == debtId || row.GetProperty("id").GetGuid() == secondDebtId);
        var userBDashboard = await ReadObject(userB, $"/api/dashboard?year={year}&month={month}");
        Assert.Equal(0m, userBDashboard.GetProperty("income").GetDecimal());
        Assert.Equal(0m, userBDashboard.GetProperty("expenses").GetDecimal());
        Assert.Equal(0m, userBDashboard.GetProperty("saved").GetDecimal());
        Assert.Empty(userBDashboard.GetProperty("budgets").EnumerateArray());
        Assert.Empty(userBDashboard.GetProperty("recent").EnumerateArray());

        await AssertStatus(userB, HttpMethod.Put, $"/api/transactions/income/{incomeId}", HttpStatusCode.NotFound,
            new { type = "income", title = "Changed by other", amount = 3000m, date = today, category = (string?)null, notes = (string?)null });
        await AssertStatus(userB, HttpMethod.Delete, $"/api/transactions/income/{incomeId}", HttpStatusCode.NotFound);
        await AssertStatus(userB, HttpMethod.Put, $"/api/transactions/expense/{expenseId}", HttpStatusCode.NotFound,
            new { type = "expense", title = "Changed by other", amount = 999m, date = today, category = "Other", notes = (string?)null });
        await AssertStatus(userB, HttpMethod.Delete, $"/api/transactions/expense/{expenseId}", HttpStatusCode.NotFound);

        await AssertStatus(userB, HttpMethod.Put, $"/api/budgets/{budgetId}", HttpStatusCode.NotFound,
            new { name = "Changed by other", limit = 999m, year, month });
        await AssertStatus(userB, HttpMethod.Delete, $"/api/budgets/{budgetId}", HttpStatusCode.NotFound);
        await AssertStatus(userB, HttpMethod.Put, $"/api/savings-goals/{goalId}", HttpStatusCode.NotFound,
            new { name = "Changed by other", targetAmount = 9000m, currentAmount = 0m, targetDate = (DateOnly?)today.AddYears(1), monthlyContribution = 0m });
        await AssertStatus(userB, HttpMethod.Delete, $"/api/savings-goals/{goalId}", HttpStatusCode.NotFound);
        await AssertStatus(userB, HttpMethod.Put, $"/api/debts/{debtId}", HttpStatusCode.NotFound,
            new { name = "Changed by other", originalAmount = 1000m, balance = 500m, interestRate = 0m, minimumPayment = 10m, frequency = "Monthly", nextDueDate = (DateOnly?)today.AddDays(30), status = "Active" });
        await AssertStatus(userB, HttpMethod.Delete, $"/api/debts/{debtId}", HttpStatusCode.NotFound);

        var paymentBody = new { amount = 50m, date = today, notes = "Attempted foreign change" };
        await AssertStatus(userB, HttpMethod.Post, $"/api/debts/{debtId}/payments", HttpStatusCode.NotFound, paymentBody);
        await AssertStatus(userB, HttpMethod.Put, $"/api/debts/{debtId}/payments/{paymentId}", HttpStatusCode.NotFound, paymentBody);
        await AssertStatus(userB, HttpMethod.Delete, $"/api/debts/{debtId}/payments/{paymentId}", HttpStatusCode.NotFound);
        // Foreign payment under User B's own debt and under a different foreign debt.
        await AssertStatus(userB, HttpMethod.Put, $"/api/debts/{userBDebtId}/payments/{paymentId}", HttpStatusCode.NotFound, paymentBody);
        await AssertStatus(userB, HttpMethod.Delete, $"/api/debts/{userBDebtId}/payments/{paymentId}", HttpStatusCode.NotFound);
        await AssertStatus(userB, HttpMethod.Put, $"/api/debts/{secondDebtId}/payments/{paymentId}", HttpStatusCode.NotFound, paymentBody);
        await AssertStatus(userB, HttpMethod.Delete, $"/api/debts/{secondDebtId}/payments/{paymentId}", HttpStatusCode.NotFound);
        // Even a payment owned by User B cannot be rebound to User B's other debt.
        await AssertStatus(userB, HttpMethod.Put, $"/api/debts/{userBSecondDebtId}/payments/{userBPaymentId}", HttpStatusCode.NotFound, paymentBody);
        await AssertStatus(userB, HttpMethod.Delete, $"/api/debts/{userBSecondDebtId}/payments/{userBPaymentId}", HttpStatusCode.NotFound);

        var ownerTransactions = await ReadArray(userA, "/api/transactions");
        Assert.Contains(ownerTransactions, row => row.GetProperty("id").GetGuid() == incomeId && row.GetProperty("amount").GetDecimal() == 2500m);
        Assert.Contains(ownerTransactions, row => row.GetProperty("id").GetGuid() == expenseId && row.GetProperty("amount").GetDecimal() == 12.34m);
        var ownerBudgets = await ReadArray(userA, $"/api/budgets?year={year}&month={month}");
        Assert.Contains(ownerBudgets, row => row.GetProperty("id").GetGuid() == budgetId && row.GetProperty("limit").GetDecimal() == 500m);
        var ownerGoals = await ReadArray(userA, "/api/savings-goals");
        Assert.Contains(ownerGoals, row => row.GetProperty("id").GetGuid() == goalId && row.GetProperty("currentAmount").GetDecimal() == 100m);
        var ownerDebts = await ReadArray(userA, "/api/debts");
        var ownerDebt = Assert.Single(ownerDebts, row => row.GetProperty("id").GetGuid() == debtId);
        Assert.Equal(900m, ownerDebt.GetProperty("balance").GetDecimal());
        var ownerPayments = ownerDebt.GetProperty("payments").EnumerateArray().ToArray();
        Assert.Contains(ownerPayments, row => row.GetProperty("id").GetGuid() == paymentId && row.GetProperty("amount").GetDecimal() == 100m);
        Assert.Contains(ownerDebts, row => row.GetProperty("id").GetGuid() == secondDebtId && row.GetProperty("balance").GetDecimal() == 500m);
        var userBDebtAfterAttempts = Assert.Single(await ReadArray(userB, "/api/debts"), row => row.GetProperty("id").GetGuid() == userBDebtId);
        Assert.Equal(575m, userBDebtAfterAttempts.GetProperty("balance").GetDecimal());
        Assert.Contains(userBDebtAfterAttempts.GetProperty("payments").EnumerateArray(), row => row.GetProperty("id").GetGuid() == userBPaymentId && row.GetProperty("amount").GetDecimal() == 25m);
    }

    private static async Task<HttpClient> NewAuthenticatedClient(SetupApiFactory factory, string label)
    {
        var client = factory.CreateClient();
        var email = $"{label}-{Guid.NewGuid():N}@example.test";
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = $"Synthetic {label}", email, password = "SyntheticTest123!"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "SyntheticTest123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> Create(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("id").GetGuid();
    }

    private static async Task AssertStatus(HttpClient client, HttpMethod method, string path, HttpStatusCode expected, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<JsonElement[]> ReadArray(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? [];
    }

    private static async Task<JsonElement> ReadObject(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
