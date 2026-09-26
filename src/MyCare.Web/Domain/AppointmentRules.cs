namespace MyCare.Web.Domain;

/// <summary>
/// The booking rules, kept free of databases and web code so unit tests can check them directly.
/// </summary>
public static class AppointmentRules
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 120;
    public const int SlotMinutes = 15;
    public const int MaxPatientNameLength = 100;
    public const int MaxReasonLength = 250;

    public static readonly TimeSpan ClinicOpens = TimeSpan.FromHours(8);
    public static readonly TimeSpan ClinicCloses = TimeSpan.FromHours(17);

    public static List<string> Validate(CreateAppointmentRequest request, DateTime nowUtc)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.PatientName))
            errors.Add("Patient name is required.");
        else if (request.PatientName.Trim().Length > MaxPatientNameLength)
            errors.Add($"Patient name must be {MaxPatientNameLength} characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.Provider) || !Providers.All.Contains(request.Provider))
            errors.Add("Choose a valid provider.");

        var duration = request.DurationMinutes;
        if (duration is null || duration < MinDurationMinutes || duration > MaxDurationMinutes || duration % SlotMinutes != 0)
            errors.Add($"Duration must be {MinDurationMinutes} to {MaxDurationMinutes} minutes, in {SlotMinutes}-minute steps.");

        if (request.Reason is { Length: > MaxReasonLength })
            errors.Add($"Reason must be {MaxReasonLength} characters or fewer.");

        if (request.Start is null)
        {
            errors.Add("Start time is required.");
            return errors;
        }

        var start = NormalizeToUtc(request.Start.Value);
        var end = start.AddMinutes(duration ?? 0);

        if (start <= nowUtc)
            errors.Add("Appointments must be in the future.");

        if (start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            errors.Add("The clinic is closed on weekends.");

        if (start.TimeOfDay < ClinicOpens || end.Date != start.Date || end.TimeOfDay > ClinicCloses)
            errors.Add("Appointments must be within clinic hours (08:00-17:00 UTC).");

        if (start.TimeOfDay.Ticks % TimeSpan.FromMinutes(SlotMinutes).Ticks != 0)
            errors.Add($"Start time must be on a {SlotMinutes}-minute boundary (e.g. 09:00, 09:15).");

        return errors;
    }

    public static bool CanCancel(Appointment appointment, DateTime nowUtc) =>
        appointment.Status == AppointmentStatus.Scheduled && appointment.Start > nowUtc;

    /// <summary>Times without a zone are treated as clinic time (UTC); local times are converted.</summary>
    public static DateTime NormalizeToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Local
            ? DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Unspecified)
            : DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
