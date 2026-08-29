using CameraVision.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Data;

/// <summary>Applies migrations and seeds the settings singletons and the admin user on startup.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(
        IDbContextFactory<AppDbContext> factory,
        IPasswordHasher<AppUser> passwordHasher,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);

        // Two processes (Web + Api) share this file; WAL persists once set.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

        if (!await db.Users.AnyAsync(ct))
        {
            var admin = new AppUser
            {
                Username = "admin",
                DisplayName = "Administrador",
                IsAdmin = true,
                IsActive = true,
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, "admin2026");
            db.Add(admin);
        }

        // Fresh databases start with one sensible rule (migrated installs keep their data).
        if (!await db.CaptureRules.AnyAsync(ct))
            db.Add(new CaptureRule { Name = "Pessoas", Classes = ["person"] });

        if (!await db.SystemSettings.AnyAsync(ct))
            db.Add(new SystemSettings { Id = 1 });

        if (!await db.HealthAlertSettings.AnyAsync(ct))
            db.Add(new HealthAlertSettings { Id = 1 });

        if (!await db.CaptureAlertSettings.AnyAsync(ct))
            db.Add(new CaptureAlertSettings { Id = 1 });

        foreach (var channel in new[] { AlertChannel.Email, AlertChannel.WhatsApp })
        {
            if (!await db.AlertSettings.AnyAsync(s => s.Channel == channel, ct))
                db.Add(new AlertSettings { Channel = channel });
        }

        await db.SaveChangesAsync(ct);
    }
}
