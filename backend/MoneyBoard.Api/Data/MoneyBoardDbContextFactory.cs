using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MoneyBoard.Api.Data;

/// <summary>Uses the same secret and environment configuration as the running API.</summary>
public sealed class MoneyBoardDbContextFactory : IDesignTimeDbContextFactory<MoneyBoardDbContext>
{
    public MoneyBoardDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<MoneyBoardDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connection = configuration.GetConnectionString("MoneyBoard");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set ConnectionStrings:MoneyBoard with .NET user-secrets or the ConnectionStrings__MoneyBoard environment variable before using EF tools.");
        var options = new DbContextOptionsBuilder<MoneyBoardDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new MoneyBoardDbContext(options);
    }
}
