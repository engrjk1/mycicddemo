using System.Net;
using System.Text.Json;
using MyCare.TestSupport;
using RestSharp;

namespace MyCare.ApiTests;

/// <summary>
/// API / contract checks: talk to a running MyCare service over HTTP, bypassing the UI.
/// Target is set by MYCARE_BASE_URL. Tests tagged "Smoke" are read-only and also run after each deployment.
/// </summary>
public sealed class AppointmentApiTests : IDisposable
{
    private readonly RestClient _client = new(new RestClientOptions(TestSettings.BaseUrl));

    public void Dispose() => _client.Dispose();

    private static RestRequest Request(string resource, Method method = Method.Get) =>
        new RestRequest(resource, method).AddHeader(TestData.ActorHeader, TestData.Actor);

    private static RestRequest BookingRequest(string? patient, string provider, DateTime start, int duration = 30) =>
        Request("api/appointments", Method.Post).AddJsonBody(new
        {
            patientName = patient,
            provider,
            start = TestData.ApiFormat(start),
            durationMinutes = duration,
            reason = "API test",
        });

    [Fact(DisplayName = "LC-109 | Health endpoint reports the service is up")]
    [Trait("Category", "Smoke")]
    public async Task Health_is_ok()
    {
        var response = await _client.ExecuteAsync(Request("api/health"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(response.Content!);
        json.RootElement.GetProperty("status").GetString().ShouldBe("ok");
    }

    [Fact(DisplayName = "LC-107 | Provider list is available")]
    [Trait("Category", "Smoke")]
    public async Task Providers_are_listed()
    {
        var response = await _client.ExecuteAsync<List<string>>(Request("api/providers"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.ShouldNotBeNull();
        response.Data.ShouldContain(TestData.ProviderA);
        response.Data.Count.ShouldBe(3);
    }

    [Fact(DisplayName = "LC-101 | Booking response matches the published appointment contract")]
    public async Task Booking_response_contract()
    {
        var response = await _client.ExecuteAsync(BookingRequest(TestData.UniquePatient(), TestData.ProviderA, TestSlots.Next()));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var json = JsonDocument.Parse(response.Content!);
        var root = json.RootElement;
        foreach (var field in new[] { "id", "patientName", "provider", "start", "end", "durationMinutes", "reason", "status" })
            root.TryGetProperty(field, out _).ShouldBeTrue($"response is missing the '{field}' field");
        root.GetProperty("id").ValueKind.ShouldBe(JsonValueKind.Number);
        root.GetProperty("status").GetString().ShouldBe("Scheduled");
    }

    [Fact(DisplayName = "LC-101 | A booked appointment can be fetched by id")]
    public async Task Booked_appointment_can_be_fetched()
    {
        var patient = TestData.UniquePatient();
        var slot = TestSlots.Next();

        var booked = await _client.ExecuteAsync<AppointmentResponse>(BookingRequest(patient, TestData.ProviderB, slot, 45));
        booked.StatusCode.ShouldBe(HttpStatusCode.Created);

        var fetched = await _client.ExecuteAsync<AppointmentResponse>(Request($"api/appointments/{booked.Data!.Id}"));
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        fetched.Data!.PatientName.ShouldBe(patient);
        fetched.Data.Start.ShouldBe(slot);
        fetched.Data.End.ShouldBe(slot.AddMinutes(45));
    }

    [Fact(DisplayName = "LC-102 | Past appointments are rejected with 400")]
    public async Task Past_booking_is_rejected()
    {
        var response = await _client.ExecuteAsync<ErrorResponse>(
            BookingRequest(TestData.UniquePatient(), TestData.ProviderA, new DateTime(2020, 1, 6, 9, 0, 0)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Data!.Errors.ShouldContain(e => e.Contains("future"));
    }

    [Fact(DisplayName = "LC-110 | Missing patient name is rejected with 400")]
    public async Task Missing_patient_is_rejected()
    {
        var response = await _client.ExecuteAsync<ErrorResponse>(BookingRequest(null, TestData.ProviderA, TestSlots.Next()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Data!.Errors.ShouldContain("Patient name is required.");
    }

    [Fact(DisplayName = "LC-105 | Double-booking a provider is rejected with 409")]
    public async Task Double_booking_is_rejected()
    {
        var slot = TestSlots.Next();
        (await _client.ExecuteAsync(BookingRequest(TestData.UniquePatient(), TestData.ProviderC, slot)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await _client.ExecuteAsync<ErrorResponse>(BookingRequest(TestData.UniquePatient(), TestData.ProviderC, slot));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        second.Data!.Errors.ShouldContain(e => e.Contains("already has an appointment"));
    }

    [Fact(DisplayName = "LC-106 | An appointment can be cancelled exactly once")]
    public async Task Cancel_appointment()
    {
        var booked = await _client.ExecuteAsync<AppointmentResponse>(BookingRequest(TestData.UniquePatient(), TestData.ProviderA, TestSlots.Next()));
        var id = booked.Data!.Id;

        var cancelled = await _client.ExecuteAsync<AppointmentResponse>(Request($"api/appointments/{id}/cancel", Method.Post));
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        cancelled.Data!.Status.ShouldBe("Cancelled");

        var again = await _client.ExecuteAsync(Request($"api/appointments/{id}/cancel", Method.Post));
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "LC-106 | Cancelling an unknown appointment returns 404")]
    public async Task Cancel_unknown_returns_404()
    {
        var response = await _client.ExecuteAsync(Request("api/appointments/999999999/cancel", Method.Post));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "LC-107 | Filtering by provider only returns that provider")]
    public async Task Filter_by_provider()
    {
        (await _client.ExecuteAsync(BookingRequest(TestData.UniquePatient(), TestData.ProviderB, TestSlots.Next())))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await _client.ExecuteAsync<List<AppointmentResponse>>(
            Request("api/appointments").AddQueryParameter("provider", TestData.ProviderB));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.ShouldNotBeNull();
        response.Data.ShouldNotBeEmpty();
        response.Data.ShouldAllBe(a => a.Provider == TestData.ProviderB);
    }
}
