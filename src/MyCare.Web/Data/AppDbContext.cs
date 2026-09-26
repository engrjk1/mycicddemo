using MyCare.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace MyCare.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var appointment = modelBuilder.Entity<Appointment>();
        appointment.Ignore(a => a.End);
        appointment.Property(a => a.PatientName).HasMaxLength(AppointmentRules.MaxPatientNameLength).IsRequired();
        appointment.Property(a => a.Provider).HasMaxLength(100).IsRequired();
        appointment.Property(a => a.Reason).HasMaxLength(AppointmentRules.MaxReasonLength);
        appointment.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        appointment.Property(a => a.CreatedBy).HasMaxLength(50);
        appointment.HasIndex(a => new { a.Provider, a.Start });
    }
}
