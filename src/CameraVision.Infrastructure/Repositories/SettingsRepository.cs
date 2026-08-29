using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class SettingsRepository(IDbContextFactory<AppDbContext> factory) : ISettingsRepository
{
    public async Task<CaptureSettings> GetCaptureSettingsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CaptureSettings.AsNoTracking().FirstOrDefaultAsync(ct)
               ?? new CaptureSettings { Id = 1 };
    }

    public async Task SaveCaptureSettingsAsync(CaptureSettings settings, CancellationToken ct = default)
    {
        settings.Id = 1;
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.CaptureSettings.FirstOrDefaultAsync(ct);
        if (existing == null)
            db.Add(settings);
        else
            db.Entry(existing).CurrentValues.SetValues(settings);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AlertSettings> GetAlertSettingsAsync(AlertChannel channel, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AlertSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Channel == channel, ct)
               ?? new AlertSettings { Channel = channel };
    }

    public async Task SaveAlertSettingsAsync(AlertSettings settings, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.AlertSettings.FirstOrDefaultAsync(s => s.Channel == settings.Channel, ct);
        if (existing == null)
        {
            db.Add(settings);
        }
        else
        {
            settings.Id = existing.Id;
            db.Entry(existing).CurrentValues.SetValues(settings);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<SystemSettings> GetSystemSettingsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(ct)
               ?? new SystemSettings { Id = 1 };
    }

    public async Task SaveSystemSettingsAsync(SystemSettings settings, CancellationToken ct = default)
    {
        settings.Id = 1;
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.SystemSettings.FirstOrDefaultAsync(ct);
        if (existing == null)
            db.Add(settings);
        else
            db.Entry(existing).CurrentValues.SetValues(settings);
        await db.SaveChangesAsync(ct);
    }
}
