using System.Runtime.CompilerServices;
using MyCare.TestSupport;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace MyCare.UiTests;

/// <summary>
/// Starts one browser for the whole test run. Which engine is chosen by the BROWSER variable
/// (chromium, firefox or webkit) - the pipeline runs all three in parallel.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Runtime { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string BrowserName { get; } = (Environment.GetEnvironmentVariable("BROWSER") ?? "chromium").ToLowerInvariant();

    public async Task InitializeAsync()
    {
        Runtime = await Playwright.CreateAsync();
        var engine = BrowserName switch
        {
            "chromium" => Runtime.Chromium,
            "firefox" => Runtime.Firefox,
            "webkit" => Runtime.Webkit,
            _ => throw new InvalidOperationException($"Unknown BROWSER '{BrowserName}'. Use chromium, firefox or webkit."),
        };
        Browser = await engine.LaunchAsync(new() { Headless = Environment.GetEnvironmentVariable("HEADED") != "1" });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Runtime.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class UiCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "ui";
}

public abstract class UiTestBase(PlaywrightFixture fixture, ITestOutputHelper output)
{
    /// <summary>
    /// Runs a test in a fresh browser context with Playwright tracing on.
    /// The trace (screenshots, DOM snapshots, network) is saved only when the test fails
    /// - "recording kept only on failure" (strategy section 4).
    /// </summary>
    protected async Task RunAsync(Func<AppointmentsPage, Task> test, [CallerMemberName] string testName = "")
    {
        var context = await fixture.Browser.NewContextAsync(new()
        {
            BaseURL = TestSettings.BaseUrl,
            ExtraHTTPHeaders = new Dictionary<string, string> { [TestData.ActorHeader] = TestData.Actor },
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });

        try
        {
            var page = await context.NewPageAsync();
            await test(new AppointmentsPage(page));
            await context.Tracing.StopAsync();
        }
        catch
        {
            var dir = Environment.GetEnvironmentVariable("PLAYWRIGHT_TRACE_DIR") ?? Path.Combine(Directory.GetCurrentDirectory(), "playwright-traces");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{fixture.BrowserName}-{testName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
            await context.Tracing.StopAsync(new() { Path = file });
            output.WriteLine($"Failure trace saved to {file}. Open it at https://trace.playwright.dev");
            throw;
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
