using CameraVision.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Data;

/// <summary>Applies migrations and seeds the settings rows, tenant and users on startup.</summary>
public static class DbInitializer
{
    /// <summary>Absorbs data with no resolvable tenant; first login of the installation.</summary>
    public const string DefaultTenantName = "Rubens Cordeiro";

    private const string TenantAdminUsername = "rubens.cordeiro@live.com.br";

    public static async Task InitializeAsync(
        IDbContextFactory<AppDbContext> factory,
        IPasswordHasher<AppUser> passwordHasher,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);

        // Two processes (Web + Api) share this file; WAL persists once set.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

        // Existing installations get the default tenant from the MultiTenancy
        // migration backfill; fresh databases create it here.
        var tenant = await db.Tenants.OrderBy(t => t.Id).FirstOrDefaultAsync(ct);
        if (tenant == null)
        {
            tenant = new Tenant { Name = DefaultTenantName };
            db.Add(tenant);
            await db.SaveChangesAsync(ct);
        }

        // There must always be a way into tenant + system management.
        if (!await db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin, ct))
        {
            var admin = new AppUser
            {
                Username = "admin",
                DisplayName = "Administrador",
                Role = UserRole.SuperAdmin,
                TenantId = null,
                IsActive = true,
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, "admin2026");
            db.Add(admin);
        }

        if (!await db.Users.AnyAsync(u => u.Username.ToLower() == TenantAdminUsername, ct))
        {
            var tenantAdmin = new AppUser
            {
                TenantId = tenant.Id,
                Username = TenantAdminUsername,
                DisplayName = "Rubens Cordeiro",
                Role = UserRole.Admin,
                IsActive = true,
            };
            tenantAdmin.PasswordHash = passwordHasher.HashPassword(tenantAdmin, "test");
            db.Add(tenantAdmin);
        }

        // Fresh databases start with one sensible rule (migrated installs keep their data).
        if (!await db.CaptureRules.AnyAsync(ct))
            db.Add(new CaptureRule { TenantId = tenant.Id, Name = "Pessoas", Classes = ["person"] });

        if (!await db.SystemSettings.AnyAsync(ct))
            db.Add(new SystemSettings { Id = 1 });

        if (!await db.HealthAlertSettings.AnyAsync(ct))
            db.Add(new HealthAlertSettings { Id = 1 });

        // Per-tenant antiflood: other tenants fall back to defaults until saved.
        if (!await db.CaptureAlertSettings.AnyAsync(s => s.TenantId == tenant.Id, ct))
            db.Add(new CaptureAlertSettings { TenantId = tenant.Id });

        foreach (var channel in new[] { AlertChannel.Email, AlertChannel.WhatsApp })
        {
            if (!await db.AlertSettings.AnyAsync(s => s.TenantId == tenant.Id && s.Channel == channel, ct))
                db.Add(new AlertSettings { TenantId = tenant.Id, Channel = channel });
        }

        await db.SaveChangesAsync(ct);
    }
}
