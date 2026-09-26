using MyCare.Web.Domain;

namespace MyCare.UnitTests;

// Every test's display name starts with the Jira requirement it covers (e.g. "LC-102 | ...").
// The pipeline fails if a test has no requirement key (traceability gate, strategy section 6).
public class AppointmentRulesTests
{
    private static readonly DateTime Now = TestClock.Monday10amUtc.UtcDateTime;

    private static CreateAppointmentRequest Valid(DateTime? start = null, int? duration = 30) =>
        new("Synthetic Patient", Providers.All[0], start ?? TestClock.Tuesday(9), duration, "Check-up");

    [Fact(DisplayName = "LC-101 | A complete, valid request passes all booking rules")]
    public void Valid_request_has_no_errors() =>
        AppointmentRules.Validate(Valid(), Now).ShouldBeEmpty();

    [Fact(DisplayName = "LC-102 | Appointments in the past are rejected")]
    public void Past_start_is_rejected() =>
        AppointmentRules.Validate(Valid(new DateTime(2030, 1, 7, 9, 0, 0)), Now)
            .ShouldContain(e => e.Contains("future"));

    [Fact(DisplayName = "LC-102 | An appointment starting right now is rejected")]
    public void Start_equal_to_now_is_rejected() =>
        AppointmentRules.Validate(Valid(new DateTime(2030, 1, 7, 10, 0, 0)), Now)
            .ShouldContain(e => e.Contains("future"));

    [Fact(DisplayName = "LC-103 | Weekend appointments are rejected")]
    public void Weekend_is_rejected() =>
        AppointmentRules.Validate(Valid(new DateTime(2030, 1, 12, 9, 0, 0)), Now)
            .ShouldContain(e => e.Contains("weekends"));

    [Theory(DisplayName = "LC-103 | Appointments outside clinic hours are rejected")]
    [InlineData(7, 45, 30)]   // starts before 08:00
    [InlineData(16, 45, 30)]  // ends at 17:15
    [InlineData(16, 0, 120)]  // ends at 18:00
    public void Outside_clinic_hours_is_rejected(int hour, int minute, int duration) =>
        AppointmentRules.Validate(Valid(TestClock.Tuesday(hour, minute), duration), Now)
            .ShouldContain(e => e.Contains("clinic hours"));

    [Fact(DisplayName = "LC-103 | An appointment ending exactly at closing time is allowed")]
    public void Ending_at_closing_time_is_allowed() =>
        AppointmentRules.Validate(Valid(TestClock.Tuesday(16, 30), 30), Now).ShouldBeEmpty();

    [Theory(DisplayName = "LC-104 | Invalid durations are rejected")]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(135)]
    public void Invalid_duration_is_rejected(int duration) =>
        AppointmentRules.Validate(Valid(duration: duration), Now)
            .ShouldContain(e => e.Contains("Duration"));

    [Fact(DisplayName = "LC-104 | A missing duration is rejected")]
    public void Missing_duration_is_rejected() =>
        AppointmentRules.Validate(Valid() with { DurationMinutes = null }, Now)
            .ShouldContain(e => e.Contains("Duration"));

    [Theory(DisplayName = "LC-104 | Valid durations are accepted")]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(120)]
    public void Valid_duration_is_accepted(int duration) =>
        AppointmentRules.Validate(Valid(duration: duration), Now).ShouldBeEmpty();

    [Fact(DisplayName = "LC-104 | Start times must be on a 15-minute boundary")]
    public void Off_slot_start_is_rejected() =>
        AppointmentRules.Validate(Valid(TestClock.Tuesday(9, 10)), Now)
            .ShouldContain(e => e.Contains("15-minute boundary"));

    [Theory(DisplayName = "LC-110 | Patient name is required")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_patient_name_is_rejected(string? name) =>
        AppointmentRules.Validate(Valid() with { PatientName = name }, Now)
            .ShouldContain("Patient name is required.");

    [Fact(DisplayName = "LC-110 | Patient names over 100 characters are rejected")]
    public void Long_patient_name_is_rejected() =>
        AppointmentRules.Validate(Valid() with { PatientName = new string('x', 101) }, Now)
            .ShouldContain(e => e.Contains("100 characters"));

    [Fact(DisplayName = "LC-110 | Unknown providers are rejected")]
    public void Unknown_provider_is_rejected() =>
        AppointmentRules.Validate(Valid() with { Provider = "Dr. Nobody" }, Now)
            .ShouldContain("Choose a valid provider.");

    [Fact(DisplayName = "LC-110 | Start time is required")]
    public void Missing_start_is_rejected() =>
        AppointmentRules.Validate(Valid() with { Start = null }, Now)
            .ShouldContain("Start time is required.");

    [Fact(DisplayName = "LC-106 | Upcoming scheduled appointments can be cancelled")]
    public void Upcoming_appointment_can_be_cancelled() =>
        AppointmentRules.CanCancel(new Appointment { Start = TestClock.Tuesday(9) }, Now).ShouldBeTrue();

    [Fact(DisplayName = "LC-106 | Past appointments cannot be cancelled")]
    public void Past_appointment_cannot_be_cancelled() =>
        AppointmentRules.CanCancel(new Appointment { Start = new DateTime(2030, 1, 7, 9, 0, 0) }, Now).ShouldBeFalse();

    [Fact(DisplayName = "LC-106 | Already-cancelled appointments cannot be cancelled again")]
    public void Cancelled_appointment_cannot_be_cancelled() =>
        AppointmentRules.CanCancel(new Appointment { Start = TestClock.Tuesday(9), Status = AppointmentStatus.Cancelled }, Now).ShouldBeFalse();
}
