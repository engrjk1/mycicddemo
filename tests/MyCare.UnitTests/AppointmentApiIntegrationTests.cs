using System.Net;
using System.Net.Http.Json;
using MyCare.Web.Api;
using MyCare.Web.Data;
using Microsoft.Extensions.DependencyInjection;

namespace MyCare.UnitTests;

/// <summary>
/// Integration checks: the real app (API + rules + database) running in memory.
/// xUnit creates a new instance per test, so every test gets its own freshly wiped database.
/// </summary>
public sealed class AppointmentApiIntegrationTests : IDisposable
{
    private const string ProviderA = "Dr. Amina Farooq";
    private const string ProviderB = "Dr. Daniel Reyes";

    private readonly MyCareAppFactory _factory = new();
    private readonly HttpClient _client;

    public AppointmentApiIntegrationTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object Booking(string patient, string provider, DateTime start, int duration = 30) =>
        new { patientName = patient, provider, start, durationMinutes = duration, reason = "Integration test" };

    private async Task<AppointmentDto> BookAsync(string patient, string provider, DateTime start, int duration = 30)
    {
        var response = await _client.PostAsJsonAsync("/api/appointments", Booking(patient, provider, start, duration));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AppointmentDto>())!;
    }

    [Fact(DisplayName = "LC-101 | Booking via the API stores the appointment and returns it")]
    public async Task Booking_is_stored()
    {
        var booked = await BookAsync("Synthetic Patient One", ProviderA, TestClock.Tuesday(9));

        var fetched = await _client.GetFromJsonAsync<AppointmentDto>($"/api/appointments/{booked.Id}");

        fetched.ShouldNotBeNull();
        fetched.PatientName.ShouldBe("Synthetic Patient One");
        fetched.Status.ShouldBe("Scheduled");
        fetched.End.ShouldBe(TestClock.Tuesday(9, 30));
    }

    [Fact(DisplayName = "LC-102 | The API rejects past appointments with a 400 and a clear message")]
    public async Task Past_booking_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/appointments", Booking("Synthetic Patient", ProviderA, new DateTime(2030, 1, 7, 9, 0, 0)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("future");
    }

    [Fact(DisplayName = "LC-105 | Overlapping bookings for the same provider are rejected (409)")]
    public async Task Double_booking_returns_409()
    {
        await BookAsync("Synthetic Patient One", ProviderA, TestClock.Tuesday(9));

        var response = await _client.PostAsJsonAsync("/api/appointments", Booking("Synthetic Patient Two", ProviderA, TestClock.Tuesday(9, 15)));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("already has an appointment");
    }

    [Fact(DisplayName = "LC-105 | Different providers can be booked at the same time")]
    public async Task Different_providers_same_time_is_allowed()
    {
        await BookAsync("Synthetic Patient One", ProviderA, TestClock.Tuesday(9));
        await BookAsync("Synthetic Patient Two", ProviderB, TestClock.Tuesday(9));
    }

    [Fact(DisplayName = "LC-105 | Back-to-back appointments do not count as overlapping")]
    public async Task Back_to_back_is_allowed()
    {
        await BookAsync("Synthetic Patient One", ProviderA, TestClock.Tuesday(9), 30);
        await BookAsync("Synthetic Patient Two", ProviderA, TestClock.Tuesday(9, 30), 30);
    }

    [Fact(DisplayName = "LC-106 | A cancelled slot can be booked again")]
    public async Task Cancelled_slot_can_be_rebooked()
    {
        var first = await BookAsync("Synthetic Patient One", ProviderA, TestClock.Tuesday(11));

        var cancel = await _client.PostAsync($"/api/appointments/{first.Id}/cancel", null);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cancel.Content.ReadFromJsonAsync<AppointmentDto>())!.Status.ShouldBe("Cancelled");

        await BookAsync("Synthetic Patient Two", ProviderA, TestClock.Tuesday(11));
    }

    [Fact(DisplayName = "LC-106 | Cancelling an unknown appointment returns 404")]
    public async Task Cancel_unknown_returns_404()
    {
        var response = await _client.PostAsync("/api/appointments/424242/cancel", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "LC-107 | Filtering by provider returns only that provider's appointments")]
    public async Task Filter_by_provider()
    {
        await BookAsync("Synthetic Patient One", ProviderA, TestClock.Tuesday(9));
        await BookAsync("Synthetic Patient Two", ProviderB, TestClock.Tuesday(10));

        var list = await _client.GetFromJsonAsync<List<AppointmentDto>>($"/api/appointments?provider={Uri.EscapeDataString(ProviderA)}");

        list.ShouldNotBeNull();
        list.ShouldHaveSingleItem();
        list[0].Provider.ShouldBe(ProviderA);
    }

    [Fact(DisplayName = "LC-108 | Automation activity is recorded separately from real users")]
    public async Task Actor_header_is_recorded()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/appointments")
        {
            Content = JsonContent.Create(Booking("Synthetic Patient One", ProviderA, TestClock.Tuesday(9))),
        };
        request.Headers.Add(AppointmentEndpoints.ActorHeader, "automation");
        var automated = await (await _client.SendAsync(request)).Content.ReadFromJsonAsync<AppointmentDto>();
        var manual = await BookAsync("Synthetic Patient Two", ProviderA, TestClock.Tuesday(10));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Appointments.Single(a => a.Id == automated!.Id).CreatedBy.ShouldBe("automation");
        db.Appointments.Single(a => a.Id == manual.Id).CreatedBy.ShouldBe("web-user");
    }
}
