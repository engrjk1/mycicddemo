using System.Globalization;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace MyCare.UiTests;

/// <summary>
/// Page object: the one place that knows how the appointments page is laid out.
/// Tests describe what a user does; if the page changes, only this file needs updating.
/// </summary>
public sealed class AppointmentsPage(IPage page)
{
    public ILocator Message => page.GetByTestId("message");
    public ILocator Rows => page.GetByTestId("appointment-row");

    public ILocator Row(string patient) => Rows.Filter(new() { HasText = patient });
    public ILocator StatusOf(string patient) => Row(patient).GetByTestId("status");

    public async Task OpenAsync()
    {
        await page.GotoAsync("/");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "MyCare Appointments" })).ToBeVisibleAsync();
    }

    public async Task BookAsync(string? patient, string provider, DateTime start, int durationMinutes = 30, string? reason = null)
    {
        if (patient is not null)
            await page.GetByTestId("patient-name").FillAsync(patient);
        await page.GetByTestId("provider").SelectOptionAsync(provider);
        await page.GetByTestId("start").FillAsync(start.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture));
        await page.GetByTestId("duration").SelectOptionAsync(durationMinutes.ToString(CultureInfo.InvariantCulture));
        if (reason is not null)
            await page.GetByTestId("reason").FillAsync(reason);
        await page.GetByTestId("book-button").ClickAsync();
    }

    public Task CancelAsync(string patient) => Row(patient).GetByTestId("cancel-button").ClickAsync();

    public Task FilterByAsync(string provider) => page.GetByTestId("provider-filter").SelectOptionAsync(provider);
}
