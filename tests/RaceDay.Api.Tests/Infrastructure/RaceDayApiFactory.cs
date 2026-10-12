using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory (same middleware, session, authentication and controllers),
/// backed by an isolated EF Core in-memory database, so tests need no SQL Server and run in CI.
/// </summary>
public class RaceDayApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@raceday.test";
    public const string AdminPassword = "AdminPass123!";

    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "InMemory");
        builder.UseSetting("Database:InMemoryName", _databaseName);
        builder.UseSetting("Seed:DemoData", "false");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);

        // Keep test output readable: the hosted API's request/EF logging is not needed in test runs.
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    /// <summary>Runs an action against the test database, e.g. to arrange data or assert what was stored.</summary>
    public async Task WithDbAsync(Func<RaceDayDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        await action(db);
    }

    public async Task<T> WithDbAsync<T>(Func<RaceDayDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        return await action(db);
    }
}
