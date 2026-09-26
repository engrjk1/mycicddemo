using MyCare.Web.Api;
using MyCare.Web.Data;
using MyCare.Web.Domain;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Render (and similar hosts) tell the app which port to listen on via PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// The connection string is read when the DbContext is created, so tests can swap in a temporary database.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default") ?? "Data Source=mycare.db"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AppointmentService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (app.Configuration.GetValue("SeedDemoData", true))
    {
        SeedData.EnsureSeeded(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapAppointmentApi();

app.Run();

// Lets the integration tests start the app in memory (WebApplicationFactory<Program>).
public partial class Program { }
