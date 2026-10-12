using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Creates/migrates the database and inserts demo data on first run so every endpoint can be tried
/// in Swagger immediately. Mirrors the sample data in docs/RaceDay_Database_Schema.sql, but with
/// real password hashes and event dates relative to today.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static async Task InitialiseAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (config.GetValue<bool>("Seed:DemoData") && !await db.Users.AnyAsync())
            await SeedDemoDataAsync(db, hasher);

        // Admin account: never self-registered, created from configuration.
        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword)
            && !await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var admin = new User { FullName = "RaceDay Admin", Email = adminEmail, RoleId = AppRoles.AdminId };
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedDemoDataAsync(RaceDayDbContext db, IPasswordHasher<User> hasher)
    {
        User NewUser(int roleId, string name, string email, string phone)
        {
            var u = new User { RoleId = roleId, FullName = name, Email = email, PhoneNumber = phone };
            u.PasswordHash = hasher.HashPassword(u, DemoPassword);
            return u;
        }

        var thabo = NewUser(AppRoles.OrganiserId, "Thabo Mokoena", "thabo.mokoena@raceday.co.za", "0821234567");
        var lindiwe = NewUser(AppRoles.OrganiserId, "Lindiwe Dube", "lindiwe.dube@raceday.co.za", "0827654321");
        var sarah = NewUser(AppRoles.ParticipantId, "Sarah Botha", "sarah.botha@example.com", "0731112222");
        var michael = NewUser(AppRoles.ParticipantId, "Michael van Wyk", "michael.vanwyk@example.com", "0733334444");
        db.Users.AddRange(thabo, lindiwe, sarah, michael);

        var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
        var marathon = new Event
        {
            Organiser = thabo, EventName = "Pretoria Spring Marathon", EventDate = today.AddDays(30),
            Location = "Pretoria, Gauteng", DistanceKm = 42.2m, EventType = EventType.Run,
            Description = "Annual road race through the Union Buildings precinct."
        };
        var nightRun = new Event
        {
            Organiser = thabo, EventName = "Centurion Night Walk", EventDate = today.AddDays(60),
            Location = "Centurion, Gauteng", DistanceKm = 5m, EventType = EventType.Walk,
            Description = "Evening fun walk with a glow-in-the-dark theme."
        };
        var trail = new Event
        {
            Organiser = lindiwe, EventName = "Cape Cycle Challenge", EventDate = today.AddDays(-14),
            Location = "Cape Town, Western Cape", DistanceKm = 60m, EventType = EventType.Cycle,
            Description = "Road cycle tour around the Cape Peninsula."
        };
        db.Events.AddRange(marathon, nightRun, trail);

        var c10k = new Category { Event = marathon, CategoryName = "10km Fun Run", DistanceKm = 10m, MaxParticipants = 500, EntryFee = 150m };
        var c42k = new Category { Event = marathon, CategoryName = "42km Marathon", DistanceKm = 42.2m, MaxParticipants = 300, EntryFee = 350m };
        var cU20 = new Category { Event = nightRun, CategoryName = "Under 20", MaxAge = 19, MaxParticipants = 200, EntryFee = 50m };
        var cSenior = new Category { Event = nightRun, CategoryName = "Senior", MinAge = 20, MaxParticipants = 400, EntryFee = 100m };
        var c30k = new Category { Event = trail, CategoryName = "30km Sprint", DistanceKm = 30m, MaxParticipants = 200, EntryFee = 250m };
        var c60k = new Category { Event = trail, CategoryName = "60km Tour", DistanceKm = 60m, MaxParticipants = 150, EntryFee = 400m };
        db.Categories.AddRange(c10k, c42k, cU20, cSenior, c30k, c60k);

        var e1 = new EventEnrolment { Participant = sarah, Category = c10k };
        var e2 = new EventEnrolment { Participant = sarah, Category = c30k };
        var e3 = new EventEnrolment { Participant = michael, Category = cSenior };
        var e4 = new EventEnrolment { Participant = michael, Category = c30k };
        db.EventEnrolments.AddRange(e1, e2, e3, e4);
        await db.SaveChangesAsync();

        foreach (var e in new[] { e1, e2, e3, e4 })
            e.BibNumber = $"RD{e.EnrolmentId:D5}";

        db.Results.AddRange(
            new Result { Enrolment = e2, FinishTime = new TimeSpan(0, 58, 41), Position = 2, Status = "Final" },
            new Result { Enrolment = e4, FinishTime = new TimeSpan(0, 55, 3), Position = 1, Status = "Final" });
        await db.SaveChangesAsync();
    }
}
