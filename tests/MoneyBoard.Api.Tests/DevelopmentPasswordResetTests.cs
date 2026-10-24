using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoneyBoard.Api.Data;
using Xunit;

namespace MoneyBoard.Api.Tests;

public sealed class DevelopmentPasswordResetTests
{
    [Fact]
    public async Task Development_reset_route_is_not_registered_outside_development()
    {
        using var factory = new SetupApiFactory("Production");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/dev/auth/reset-password", new
        {
            email = "existing@example.test",
            confirmEmail = "existing@example.test",
            newPassword = "NewPassword123!"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Development_reset_rejects_unknown_email_without_creating_an_account()
    {
        using var factory = new SetupApiFactory("Development");
        var client = factory.CreateClient();
        await factory.EnsureDatabaseAsync();

        var response = await client.PostAsJsonAsync("/api/dev/auth/reset-password", new
        {
            email = "missing@example.test",
            confirmEmail = "missing@example.test",
            newPassword = "NewPassword123!"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task Development_reset_changes_only_the_password_of_the_single_matching_account()
    {
        using var factory = new SetupApiFactory("Development");
        var client = factory.CreateClient();
        await factory.EnsureDatabaseAsync();
        var email = $"reset-{Guid.NewGuid():N}@example.test";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Reset test",
            email,
            password = "OriginalPassword123!"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        string previousHash;
        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
            var user = await db.Users.SingleAsync(x => x.NormalizedEmail == email.ToUpperInvariant());
            userId = user.Id;
            previousHash = user.PasswordHash!;
        }

        var response = await client.PostAsJsonAsync("/api/dev/auth/reset-password", new
        {
            email = $" {email.ToUpperInvariant()} ",
            confirmEmail = email.ToUpperInvariant(),
            newPassword = "ReplacementPassword456!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Password reset. You can now sign in.", body.GetProperty("message").GetString());
        Assert.DoesNotContain("token", body.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body.ToString(), StringComparison.OrdinalIgnoreCase);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "ReplacementPassword456!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
            Assert.Equal(1, await db.Users.CountAsync());
            var user = await db.Users.SingleAsync();
            Assert.Equal(userId, user.Id);
            Assert.NotEqual(previousHash, user.PasswordHash);
            Assert.Empty(await db.Incomes.ToListAsync());
            Assert.Empty(await db.Expenses.ToListAsync());
            Assert.Empty(await db.Budgets.ToListAsync());
            Assert.Empty(await db.SavingsGoals.ToListAsync());
            Assert.Empty(await db.Debts.ToListAsync());
        }
    }
}
