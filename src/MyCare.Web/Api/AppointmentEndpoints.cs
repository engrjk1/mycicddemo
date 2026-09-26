using MyCare.Web.Domain;

namespace MyCare.Web.Api;

public record AppointmentDto(
    int Id,
    string PatientName,
    string Provider,
    DateTime Start,
    DateTime End,
    int DurationMinutes,
    string? Reason,
    string Status);

public static class AppointmentEndpoints
{
    /// <summary>Clients identify themselves here; automated tests send "automation" so their activity is distinguishable in audit logs.</summary>
    public const string ActorHeader = "X-Actor";

    public static void MapAppointmentApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            version = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT") is { Length: > 0 } commit ? commit : "local",
        }));

        api.MapGet("/providers", () => Providers.All);

        api.MapGet("/appointments", async (string? provider, AppointmentService service) =>
            (await service.ListAsync(provider)).Select(ToDto));

        api.MapGet("/appointments/{id:int}", async Task<IResult> (int id, AppointmentService service) =>
            await service.GetAsync(id) is { } appointment ? Results.Ok(ToDto(appointment)) : Results.NotFound());

        api.MapPost("/appointments", async Task<IResult> (
            CreateAppointmentRequest request, HttpRequest http, AppointmentService service, ILogger<AppointmentService> log) =>
        {
            var actor = ResolveActor(http);
            var result = await service.BookAsync(request, actor);
            switch (result.Outcome)
            {
                case BookingOutcome.Booked:
                    var appointment = result.Appointment!;
                    // Audit log: IDs and actor only - never patient details.
                    log.LogInformation("Appointment {Id} booked with {Provider} by actor {Actor}", appointment.Id, appointment.Provider, actor);
                    return Results.Created($"/api/appointments/{appointment.Id}", ToDto(appointment));
                case BookingOutcome.Conflict:
                    return Results.Conflict(new { errors = result.Errors });
                default:
                    return Results.BadRequest(new { errors = result.Errors });
            }
        });

        api.MapPost("/appointments/{id:int}/cancel", async Task<IResult> (
            int id, HttpRequest http, AppointmentService service, ILogger<AppointmentService> log) =>
        {
            var (outcome, appointment) = await service.CancelAsync(id);
            switch (outcome)
            {
                case CancelOutcome.Cancelled:
                    log.LogInformation("Appointment {Id} cancelled by actor {Actor}", id, ResolveActor(http));
                    return Results.Ok(ToDto(appointment!));
                case CancelOutcome.NotFound:
                    return Results.NotFound();
                default:
                    return Results.BadRequest(new { errors = new[] { "Only upcoming scheduled appointments can be cancelled." } });
            }
        });
    }

    private static string ResolveActor(HttpRequest http)
    {
        var actor = http.Headers[ActorHeader].ToString().Trim();
        return actor.Length == 0 ? "web-user" : actor[..Math.Min(actor.Length, 50)];
    }

    private static AppointmentDto ToDto(Appointment a) =>
        new(a.Id, a.PatientName, a.Provider, a.Start, a.End, a.DurationMinutes, a.Reason, a.Status.ToString());
}
