using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class CameraHealthEventRepository(IDbContextFactory<AppDbContext> factory) : ICameraHealthEventRepository
{
    public async Task AddAsync(CameraHealthEvent healthEvent, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CameraHealthEvents.Add(healthEvent);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CameraHealthEvent>> GetRecentByCameraAsync(
        int cameraId, int take, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CameraHealthEvents.AsNoTracking()
            .Where(h => h.CameraId == cameraId)
            .OrderByDescending(h => h.OccurredAt).ThenByDescending(h => h.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountNotifiedSinceAsync(DateTime since, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CameraHealthEvents.AsNoTracking()
            .CountAsync(h => h.NotifiedAt != null && h.NotifiedAt >= since, ct);
    }

    public async Task<DateTime?> GetLastNotifiedAtAsync(
        string cameraName, HealthCondition condition, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CameraHealthEvents.AsNoTracking()
            .Where(h => h.CameraName == cameraName && h.Condition == condition && h.NotifiedAt != null)
            .MaxAsync(h => h.NotifiedAt, ct);
    }

    public async Task<IReadOnlyList<CameraHealthEvent>> GetPendingForDigestAsync(
        int take, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CameraHealthEvents.AsNoTracking()
            .Where(h => h.NotifiedAt == null && h.DigestedAt == null)
            .OrderBy(h => h.OccurredAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task MarkDigestedAsync(IEnumerable<int> ids, DateTime digestedAt, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return;
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.CameraHealthEvents
            .Where(h => idList.Contains(h.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.DigestedAt, digestedAt), ct);
    }
}
