using MyCare.Web.Domain;

namespace MyCare.Web.Data;

/// <summary>Adds a few synthetic appointments so a fresh demo site isn't empty. No real patient data, ever.</summary>
public static class SeedData
{
    public static void EnsureSeeded(AppDbContext db, TimeProvider time)
    {
        if (db.Appointments.Any())
            return;

        var now = time.GetUtcNow().UtcDateTime;
        var day1 = NextWeekday(now.Date.AddDays(1));
        var day2 = NextWeekday(day1.AddDays(1));

        db.Appointments.AddRange(
            Create("Sample Patient Alpha", Providers.All[0], day1.AddHours(9), 30, "Annual check-up", now),
            Create("Sample Patient Bravo", Providers.All[1], day1.AddHours(10.5), 45, "Follow-up visit", now),
            Create("Sample Patient Charlie", Providers.All[2], day2.AddHours(14), 60, "Physical therapy", now));
        db.SaveChanges();
    }

    private static DateTime NextWeekday(DateTime day)
    {
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        return day;
    }

    private static Appointment Create(string patient, string provider, DateTime start, int minutes, string reason, DateTime now) => new()
    {
        PatientName = patient,
        Provider = provider,
        Start = start,
        DurationMinutes = minutes,
        Reason = reason,
        CreatedBy = "seed",
        CreatedAtUtc = now,
    };
}
