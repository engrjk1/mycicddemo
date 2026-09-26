using MyCare.TestSupport;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace MyCare.UiTests;

/// <summary>
/// UI regression: full user journeys in a real browser against a running MyCare site (MYCARE_BASE_URL).
/// To quarantine a flaky test, add [Trait("Category", "Quarantine")]; it then runs without blocking releases.
/// </summary>
[Collection(UiCollection.Name)]
public class AppointmentJourneyTests(PlaywrightFixture fixture, ITestOutputHelper output) : UiTestBase(fixture, output)
{
    [Fact(DisplayName = "LC-101 | Patient can book an appointment and see it in the list")]
    public Task Book_appointment() => RunAsync(async app =>
    {
        var patient = TestData.UniquePatient();
        await app.OpenAsync();

        await app.BookAsync(patient, TestData.ProviderA, TestSlots.Next(), 30, "Annual check-up");

        await Expect(app.Message).ToContainTextAsync("Booked");
        await Expect(app.Row(patient)).ToHaveCountAsync(1);
        await Expect(app.StatusOf(patient)).ToHaveTextAsync("Scheduled");
    });

    [Fact(DisplayName = "LC-102 | Booking in the past shows an error and nothing is saved")]
    public Task Past_booking_shows_error() => RunAsync(async app =>
    {
        var patient = TestData.UniquePatient();
        await app.OpenAsync();

        await app.BookAsync(patient, TestData.ProviderA, new DateTime(2020, 1, 6, 9, 0, 0));

        await Expect(app.Message).ToContainTextAsync("must be in the future");
        await Expect(app.Row(patient)).ToHaveCountAsync(0);
    });

    [Fact(DisplayName = "LC-105 | Double-booking a provider is blocked with a clear message")]
    public Task Double_booking_is_blocked() => RunAsync(async app =>
    {
        var slot = TestSlots.Next();
        var first = TestData.UniquePatient("First");
        var second = TestData.UniquePatient("Second");
        await app.OpenAsync();

        await app.BookAsync(first, TestData.ProviderC, slot);
        await Expect(app.Message).ToContainTextAsync("Booked");

        await app.BookAsync(second, TestData.ProviderC, slot);
        await Expect(app.Message).ToContainTextAsync("already has an appointment");
        await Expect(app.Row(second)).ToHaveCountAsync(0);
    });

    [Fact(DisplayName = "LC-106 | Patient can cancel an upcoming appointment")]
    public Task Cancel_appointment() => RunAsync(async app =>
    {
        var patient = TestData.UniquePatient();
        await app.OpenAsync();
        await app.BookAsync(patient, TestData.ProviderB, TestSlots.Next());
        await Expect(app.StatusOf(patient)).ToHaveTextAsync("Scheduled");

        await app.CancelAsync(patient);

        await Expect(app.StatusOf(patient)).ToHaveTextAsync("Cancelled");
        await Expect(app.Row(patient).GetByTestId("cancel-button")).ToHaveCountAsync(0);
    });

    [Fact(DisplayName = "LC-107 | Filtering by provider hides other providers' appointments")]
    public Task Filter_by_provider() => RunAsync(async app =>
    {
        var patient = TestData.UniquePatient();
        await app.OpenAsync();
        await app.BookAsync(patient, TestData.ProviderB, TestSlots.Next());
        await Expect(app.Row(patient)).ToHaveCountAsync(1);

        await app.FilterByAsync(TestData.ProviderA);
        await Expect(app.Row(patient)).ToHaveCountAsync(0);

        await app.FilterByAsync(TestData.ProviderB);
        await Expect(app.Row(patient)).ToHaveCountAsync(1);
    });

    [Fact(DisplayName = "LC-110 | Submitting without a patient name shows a validation message")]
    public Task Missing_name_shows_validation() => RunAsync(async app =>
    {
        await app.OpenAsync();

        await app.BookAsync(null, TestData.ProviderA, TestSlots.Next());

        await Expect(app.Message).ToContainTextAsync("Patient name is required");
    });
}
