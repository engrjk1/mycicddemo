namespace MyCare.Web.Domain;

/// <summary>What a client sends to book an appointment. Everything is nullable so missing fields become validation messages, not crashes.</summary>
public record CreateAppointmentRequest(
    string? PatientName,
    string? Provider,
    DateTime? Start,
    int? DurationMinutes,
    string? Reason);
