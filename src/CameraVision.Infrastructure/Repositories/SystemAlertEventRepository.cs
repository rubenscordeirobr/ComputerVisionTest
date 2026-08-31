using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class SystemAlertEventRepository(IDbContextFactory<AppDbContext> factory) : ISystemAlertEventRepository
{
    public async Task AddAsync(SystemAlertEvent alertEvent, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Add(alertEvent);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SystemAlertEvent>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.SystemAlertEvents.AsNoTracking()
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<SystemAlertEvent?> GetLastAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.SystemAlertEvents.AsNoTracking()
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DateTime?> GetLastNotifiedAtAsync(SystemAlertType type, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.SystemAlertEvents.AsNoTracking()
            .Where(e => e.Type == type && e.NotifiedAt != null)
            .MaxAsync(e => e.NotifiedAt, ct);
    }
}
