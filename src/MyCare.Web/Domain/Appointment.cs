namespace MyCare.Web.Domain;

public enum AppointmentStatus
{
    Scheduled,
    Cancelled,
}

public class Appointment
{
    public int Id { get; set; }
    public string PatientName { get; set; } = "";
    public string Provider { get; set; } = "";

    /// <summary>Start time in clinic time (UTC for this demo).</summary>
    public DateTime Start { get; set; }

    public int DurationMinutes { get; set; }
    public string? Reason { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    /// <summary>Who created the record ("web-user", "automation", "seed") so test activity is distinguishable in audit trails.</summary>
    public string CreatedBy { get; set; } = "web-user";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime End => Start.AddMinutes(DurationMinutes);
}
