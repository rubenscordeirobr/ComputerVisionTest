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

        if (!await db.CaptureSettings.AnyAsync(ct))
            db.Add(new CaptureSettings { Id = 1 });

        if (!await db.SystemSettings.AnyAsync(ct))
            db.Add(new SystemSettings { Id = 1 });

        foreach (var channel in new[] { AlertChannel.Email, AlertChannel.WhatsApp })
        {
            if (!await db.AlertSettings.AnyAsync(s => s.Channel == channel, ct))
                db.Add(new AlertSettings { Channel = channel });
        }

        await db.SaveChangesAsync(ct);
    }
}
