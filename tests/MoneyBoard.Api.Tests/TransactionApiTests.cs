using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoneyBoard.Api.Data;
using Xunit;

namespace MoneyBoard.Api.Tests;

public sealed class TransactionApiTests
{
    [Fact]
    public async Task Creates_income_and_expenses_with_user_categories_and_normalized_category_reuse()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);

        var income = await client.PostAsJsonAsync("/api/transactions", new { type = "income", title = " Salary ", amount = 2850m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = "monthly" });
        Assert.Equal(HttpStatusCode.Created, income.StatusCode);
        var incomeView = await income.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Salary", incomeView.GetProperty("title").GetString());
        Assert.Equal("Income", incomeView.GetProperty("category").GetString());
        Assert.Equal(2850m, incomeView.GetProperty("amount").GetDecimal());

        var expense = await client.PostAsJsonAsync("/api/transactions", new { type = "expense", title = " Plants ", amount = 24.5m, date = new DateOnly(2026, 10, 2), category = " Garden ", notes = "patio" });
        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);
        var expenseView = await expense.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Garden", expenseView.GetProperty("category").GetString());
        Assert.Equal(new DateOnly(2026, 10, 2), expenseView.GetProperty("date").Deserialize<DateOnly>());

        var another = await client.PostAsJsonAsync("/api/transactions", new { type = "expense", title = "Seeds", amount = 3m, date = new DateOnly(2026, 10, 3), category = "garden", notes = (string?)null });
        Assert.Equal(HttpStatusCode.Created, another.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
        Assert.Equal(1, await db.ExpenseCategories.CountAsync());
        Assert.Equal(2, await db.Expenses.CountAsync());
        Assert.Single(await db.Incomes.ToListAsync());
    }

    [Fact]
    public async Task Edits_income_and_expense_fields_and_category()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        var incomeCreate = await client.PostAsJsonAsync("/api/transactions", new { type = "income", title = "Old source", amount = 20m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = (string?)null });
        var incomeId = (await incomeCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var incomeUpdate = await client.PutAsJsonAsync($"/api/transactions/income/{incomeId}", new { type = "income", title = "New source", amount = 45m, date = new DateOnly(2026, 10, 8), category = (string?)null, notes = "updated" });
        Assert.Equal(HttpStatusCode.OK, incomeUpdate.StatusCode);
        var income = await incomeUpdate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("New source", income.GetProperty("title").GetString());
        Assert.Equal(45m, income.GetProperty("amount").GetDecimal());
        Assert.Equal("updated", income.GetProperty("notes").GetString());

        var expenseCreate = await client.PostAsJsonAsync("/api/transactions", new { type = "expense", title = "Old item", amount = 12m, date = new DateOnly(2026, 10, 2), category = "Food", notes = (string?)null });
        var expenseId = (await expenseCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var expenseUpdate = await client.PutAsJsonAsync($"/api/transactions/expense/{expenseId}", new { type = "expense", title = "New item", amount = 19.99m, date = new DateOnly(2026, 10, 9), category = "Garden", notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, expenseUpdate.StatusCode);
        var expense = await expenseUpdate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("New item", expense.GetProperty("title").GetString());
        Assert.Equal("Garden", expense.GetProperty("category").GetString());
        var invalidEdit = await client.PutAsJsonAsync($"/api/transactions/expense/{expenseId}", new { type = "expense", title = "Should not save", amount = 0m, date = new DateOnly(2026, 10, 10), category = "Other", notes = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, invalidEdit.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
        Assert.Equal(45m, (await db.Incomes.SingleAsync()).Amount);
        var savedExpense = await db.Expenses.Include(x => x.Category).SingleAsync();
        Assert.Equal(19.99m, savedExpense.Amount);
        Assert.Equal("Garden", savedExpense.Category!.Name);
    }

    [Fact]
    public async Task Rejects_url_body_type_mismatch_without_changing_either_transaction()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        var incomeResponse = await client.PostAsJsonAsync("/api/transactions", new { type = "income", title = "Pay", amount = 80m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = (string?)null });
        var incomeId = (await incomeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var expenseResponse = await client.PostAsJsonAsync("/api/transactions", new { type = "expense", title = "Food", amount = 10m, date = new DateOnly(2026, 10, 2), category = "Food", notes = (string?)null });
        var expenseId = (await expenseResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var incomeMismatch = await client.PutAsJsonAsync($"/api/transactions/income/{incomeId}", new { type = "expense", title = "Changed", amount = 99m, date = new DateOnly(2026, 10, 3), category = "Food", notes = (string?)null });
        var expenseMismatch = await client.PutAsJsonAsync($"/api/transactions/expense/{expenseId}", new { type = "income", title = "Changed", amount = 99m, date = new DateOnly(2026, 10, 3), category = (string?)null, notes = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, incomeMismatch.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, expenseMismatch.StatusCode);
        Assert.Contains("type", (await incomeMismatch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateObject().Select(x => x.Name));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
        Assert.Equal((80m, "Pay"), await db.Incomes.Select(x => new ValueTuple<decimal, string>(x.Amount, x.Source)).SingleAsync());
        Assert.Equal((10m, "Food"), await db.Expenses.Select(x => new ValueTuple<decimal, string>(x.Amount, x.Category!.Name)).SingleAsync());
        Assert.Single(await db.ExpenseCategories.ToListAsync());
    }

    [Fact]
    public async Task Rejects_invalid_create_inputs_without_persisting_records()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        var invalidBodies = new object[]
        {
            new { type = "other", title = "x", amount = 1m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = (string?)null },
            new { type = "income", title = "x", amount = 0m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = (string?)null },
            new { type = "income", title = "x", amount = -1m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = (string?)null },
            new { type = "income", title = "x", amount = 1.234m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = (string?)null },
            new { type = "income", title = " ", amount = 1m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = (string?)null },
            new { type = "expense", title = "x", amount = 1m, date = new DateOnly(2026, 10, 1), category = (string?)null, notes = (string?)null },
            new { type = "expense", title = "x", amount = 1m, date = new DateOnly(1, 1, 1), category = "Food", notes = (string?)null },
            new { type = "expense", title = "x", amount = 1m, date = new DateOnly(2026, 10, 1), category = new string('a', 61), notes = (string?)null }
        };

        foreach (var body in invalidBodies)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transactions", body)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
        Assert.Empty(await db.Incomes.ToListAsync());
        Assert.Empty(await db.Expenses.ToListAsync());
        Assert.Empty(await db.ExpenseCategories.ToListAsync());
    }

    [Fact]
    public async Task Delete_removes_both_transaction_types_and_month_boundary_dates_are_persisted()
    {
        using var factory = new SetupApiFactory();
        var client = await NewAuthenticatedClient(factory);
        var incomeIds = new List<Guid>();
        foreach (var (day, amount) in new[] { (30, 75m), (1, 100m), (31, 50m), (1, 300m) })
        {
            var month = incomeIds.Count switch { 0 => 9, 1 => 10, 2 => 10, _ => 11 };
            var response = await client.PostAsJsonAsync("/api/transactions", new { type = "income", title = "Pay", amount, date = new DateOnly(2026, month, day), category = (string?)null, notes = (string?)null });
            incomeIds.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }
        var expenseIds = new List<Guid>();
        foreach (var (month, day, amount, category) in new[] { (9, 30, 100m, "Food"), (10, 1, 10m, "Food"), (10, 31, 20m, "Transit"), (11, 1, 40m, "Food") })
        {
            var response = await client.PostAsJsonAsync("/api/transactions", new { type = "expense", title = "Purchase", amount, date = new DateOnly(2026, month, day), category, notes = (string?)null });
            expenseIds.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        var listed = await client.GetFromJsonAsync<JsonElement[]>("/api/transactions");
        Assert.Equal(8, listed!.Length);
        Assert.Contains(listed, x => x.GetProperty("date").Deserialize<DateOnly>() == new DateOnly(2026, 10, 31));
        Assert.Contains(listed, x => x.GetProperty("date").Deserialize<DateOnly>() == new DateOnly(2026, 11, 1));
        Assert.Contains(listed, x => x.GetProperty("date").Deserialize<DateOnly>() == new DateOnly(2026, 9, 30));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/income/{incomeIds[3]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/expense/{expenseIds[3]}")).StatusCode);
        listed = await client.GetFromJsonAsync<JsonElement[]>("/api/transactions");
        Assert.Equal(6, listed!.Length);
        Assert.DoesNotContain(listed, x => x.GetProperty("date").Deserialize<DateOnly>() == new DateOnly(2026, 11, 1));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/income/{incomeIds[1]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/expense/{expenseIds[1]}")).StatusCode);
        var afterDelete = await client.GetFromJsonAsync<JsonElement[]>("/api/transactions");
        Assert.Equal(4, afterDelete!.Length);
        Assert.DoesNotContain(afterDelete, x => x.GetProperty("date").Deserialize<DateOnly>() == new DateOnly(2026, 10, 1));
    }

    private static async Task<HttpClient> NewAuthenticatedClient(SetupApiFactory factory)
    {
        var client = factory.CreateClient();
        await factory.EnsureDatabaseAsync();
        var email = $"transactions-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { name = "Transactions", email, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
