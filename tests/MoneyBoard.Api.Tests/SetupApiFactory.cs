using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoneyBoard.Api.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace MoneyBoard.Api.Tests;

public sealed class SetupApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new($"Data Source=moneyboard-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
    private readonly string _environment;
    public TestSaveChangesInterceptor SaveChangesInterceptor { get; } = new();

    public SetupApiFactory(string environment = "Testing")
    {
        _environment = environment;
        // Minimal hosting executes Program before ConfigureWebHost's app configuration callback.
        // These process-local values satisfy startup validation; the SQL provider is replaced below.
        Environment.SetEnvironmentVariable("ConnectionStrings__MoneyBoard", "Data Source=isolated-test-database");
        Environment.SetEnvironmentVariable("Authentication__SigningKey", "isolated-test-signing-key-longer-than-32-bytes");
        Environment.SetEnvironmentVariable("Authentication__Issuer", "MoneyBoard");
        Environment.SetEnvironmentVariable("Authentication__Audience", "MoneyBoard");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MoneyBoard"] = "Data Source=isolated-test-database",
            ["Authentication:SigningKey"] = "isolated-test-signing-key-longer-than-32-bytes",
            ["Authentication:Issuer"] = "MoneyBoard",
            ["Authentication:Audience"] = "MoneyBoard"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, LoopbackTestRemoteAddressStartupFilter>();
            foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType.IsGenericType
                && descriptor.ServiceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1"
                && descriptor.ServiceType.GetGenericArguments()[0] == typeof(MoneyBoardDbContext)).ToArray())
                services.Remove(descriptor);
            services.RemoveAll<DbContextOptions<MoneyBoardDbContext>>();
            services.RemoveAll<MoneyBoardDbContext>();
            services.AddSingleton(SaveChangesInterceptor);
            services.AddDbContext<MoneyBoardDbContext>((provider, options) =>
                options.UseSqlite(_connection).AddInterceptors(provider.GetRequiredService<TestSaveChangesInterceptor>()));
        });
    }

    public async Task EnsureDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBoardDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

internal sealed class LoopbackTestRemoteAddressStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress ??= System.Net.IPAddress.Loopback;
            await nextMiddleware();
        });
        next(app);
    };
}

public sealed class TestSaveChangesInterceptor : SaveChangesInterceptor
{
    private int _failNext;
    public void FailNextSave() => Interlocked.Exchange(ref _failNext, 1);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _failNext, 0) == 1) throw new InvalidOperationException("Test injected a persistence failure.");
        return ValueTask.FromResult(result);
    }
}
