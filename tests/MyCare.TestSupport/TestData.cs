using System.Globalization;

namespace MyCare.TestSupport;

/// <summary>Synthetic test data only - never real patient data (strategy sections 5 and 7).</summary>
public static class TestData
{
    /// <summary>Sent on every request so the app's audit log can tell test activity from real users.</summary>
    public const string ActorHeader = "X-Actor";
    public const string Actor = "automation";

    public const string ProviderA = "Dr. Amina Farooq";
    public const string ProviderB = "Dr. Daniel Reyes";
    public const string ProviderC = "Dr. Sofia Chen";

    public static string UniquePatient(string label = "Patient") =>
        $"Synthetic {label} {Guid.NewGuid().ToString("N")[..8]}";

    public static string ApiFormat(DateTime value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// Hands out free appointment slots so tests never collide with each other or with earlier runs:
/// a random weekday far in the future, then 30-minute steps through clinic hours.
/// </summary>
public static class TestSlots
{
    private const int SlotsPerDay = 16; // 08:00 .. 15:30
    private static readonly DateTime BaseDay = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(60, 3000));
    private static int _counter = -1;

    public static DateTime Next()
    {
        var n = Interlocked.Increment(ref _counter);
        var day = AddWeekdays(BaseDay, n / SlotsPerDay);
        return day.AddHours(8).AddMinutes(30 * (n % SlotsPerDay));
    }

    private static DateTime AddWeekdays(DateTime start, int weekdays)
    {
        var day = start;
        while (IsWeekend(day)) day = day.AddDays(1);
        for (var i = 0; i < weekdays; i++)
        {
            do day = day.AddDays(1);
            while (IsWeekend(day));
        }
        return day;
    }

    private static bool IsWeekend(DateTime day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}

public record AppointmentResponse(
    int Id,
    string PatientName,
    string Provider,
    DateTime Start,
    DateTime End,
    int DurationMinutes,
    string? Reason,
    string Status);

public record ErrorResponse(string[] Errors);
