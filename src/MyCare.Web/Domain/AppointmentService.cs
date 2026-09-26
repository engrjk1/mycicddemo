using MyCare.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace MyCare.Web.Domain;

public enum BookingOutcome
{
    Booked,
    Invalid,
    Conflict,
}

public sealed record BookingResult(BookingOutcome Outcome, Appointment? Appointment, IReadOnlyList<string> Errors)
{
    public static BookingResult Success(Appointment appointment) => new(BookingOutcome.Booked, appointment, Array.Empty<string>());
    public static BookingResult Invalid(IReadOnlyList<string> errors) => new(BookingOutcome.Invalid, null, errors);
    public static BookingResult Conflict(string error) => new(BookingOutcome.Conflict, null, new[] { error });
}

public enum CancelOutcome
{
    Cancelled,
    NotFound,
    NotAllowed,
}

public sealed class AppointmentService(AppDbContext db, TimeProvider time)
{
    public Task<List<Appointment>> ListAsync(string? provider)
    {
        var query = db.Appointments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(provider))
            query = query.Where(a => a.Provider == provider);
        return query.OrderBy(a => a.Start).ToListAsync();
    }

    public async Task<Appointment?> GetAsync(int id) => await db.Appointments.FindAsync(id);

    public async Task<BookingResult> BookAsync(CreateAppointmentRequest request, string actor)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var errors = AppointmentRules.Validate(request, now);
        if (errors.Count > 0)
            return BookingResult.Invalid(errors);

        var provider = request.Provider!;
        var start = AppointmentRules.NormalizeToUtc(request.Start!.Value);
        var end = start.AddMinutes(request.DurationMinutes!.Value);

        // Appointments never cross midnight (clinic hours), so only the same day can overlap.
        var dayStart = start.Date;
        var dayEnd = dayStart.AddDays(1);
        var sameDay = await db.Appointments
            .Where(a => a.Provider == provider && a.Status == AppointmentStatus.Scheduled && a.Start >= dayStart && a.Start < dayEnd)
            .ToListAsync();

        if (sameDay.Any(a => a.Start < end && start < a.End))
            return BookingResult.Conflict($"{provider} already has an appointment at that time. Choose another time.");

        var appointment = new Appointment
        {
            PatientName = request.PatientName!.Trim(),
            Provider = provider,
            Start = start,
            DurationMinutes = request.DurationMinutes.Value,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            CreatedBy = actor,
            CreatedAtUtc = now,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return BookingResult.Success(appointment);
    }

    public async Task<(CancelOutcome Outcome, Appointment? Appointment)> CancelAsync(int id)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null)
            return (CancelOutcome.NotFound, null);

        if (!AppointmentRules.CanCancel(appointment, time.GetUtcNow().UtcDateTime))
            return (CancelOutcome.NotAllowed, appointment);

        appointment.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return (CancelOutcome.Cancelled, appointment);
    }
}
