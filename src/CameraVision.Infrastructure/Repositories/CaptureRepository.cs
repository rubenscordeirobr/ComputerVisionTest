using CameraVision.Core;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class CaptureRepository(IDbContextFactory<AppDbContext> factory) : ICaptureRepository
{
    public async Task<CapturePage> QueryAsync(CaptureFilter filter, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Captures.AsNoTracking();

        if (filter.DateFrom is { } from)
            query = query.Where(c => c.StartedAt >= from);
        if (filter.DateTo is { } to)
            query = query.Where(c => c.StartedAt < to);
        if (!string.IsNullOrWhiteSpace(filter.CameraName))
            query = query.Where(c => c.CameraName == filter.CameraName);
        if (!string.IsNullOrWhiteSpace(filter.ObjectClass))
            query = query.Where(c => c.ObjectClass == filter.ObjectClass);
        if (filter.TrackId is { } trackId)
            query = query.Where(c => c.TrackId == trackId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.StartedAt).ThenByDescending(c => c.Id)
            .Skip(filter.Skip).Take(filter.Take)
            .ToListAsync(ct);

        return new CapturePage(items, total);
    }

    public async Task<Capture?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Captures.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task AddRangeAsync(IEnumerable<Capture> captures, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Captures.AddRange(captures);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Captures.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetKnownFilePathsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Captures.AsNoTracking().Select(c => c.FilePath).ToListAsync(ct);
    }

    public async Task<int> RemoveByFilePathsAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        var paths = filePaths.ToList();
        if (paths.Count == 0)
            return 0;
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Captures.Where(c => paths.Contains(c.FilePath)).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDistinctCameraNamesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Captures.AsNoTracking()
            .Select(c => c.CameraName).Distinct().OrderBy(n => n).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDistinctClassesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Captures.AsNoTracking()
            .Select(c => c.ObjectClass).Distinct().OrderBy(n => n).ToListAsync(ct);
    }

    public async Task<int> CountAsync(DateTime? startedAtOrAfter = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Captures.AsNoTracking();
        if (startedAtOrAfter is { } from)
            query = query.Where(c => c.StartedAt >= from);
        return await query.CountAsync(ct);
    }
}
