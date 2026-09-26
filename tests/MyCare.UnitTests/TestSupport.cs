using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyCare.UnitTests;

/// <summary>A clock the tests control, so "now" never drifts.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public static class TestClock
{
    /// <summary>Monday 7 January 2030, 10:00 UTC.</summary>
    public static readonly DateTimeOffset Monday10amUtc = new(2030, 1, 7, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Tuesday 8 January 2030 at the given clinic time.</summary>
    public static DateTime Tuesday(int hour, int minute = 0) => new(2030, 1, 8, hour, minute, 0);
}

/// <summary>
/// Starts the real app in memory with a temporary SQLite database that is wiped when the test finishes.
/// No connection to any real system (strategy section 4).
/// </summary>
public sealed class MyCareAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mycare-test-{Guid.NewGuid():N}.db");

    public FixedTimeProvider Clock { get; } = new(TestClock.Monday10amUtc);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
            ["SeedDemoData"] = "false",
        }));
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
