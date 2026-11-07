using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Sets up the database on start-up and adds the Admin account if it is configured.
/// Organisers and Participants sign up through the register page.
/// </summary>
public static class DbSeeder
{
    public static async Task InitialiseAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        // Optional Admin account. Admin cannot be self-registered, so it is only created when both values are
        // supplied through user secrets or environment variables (Seed__AdminEmail / Seed__AdminPassword).
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
}
