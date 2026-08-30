using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class CaptureAlertLogRepository(IDbContextFactory<AppDbContext> factory) : ICaptureAlertLogRepository
{
    public async Task AddRangeAsync(IEnumerable<CaptureAlertLog> logs, CancellationToken ct = default)
    {
        var rows = logs.ToList();
        if (rows.Count == 0)
            return;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CaptureAlertLogs.AddRange(rows);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CaptureAlertLogEntry>> GetByCaptureAsync(int captureId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // Inner join is safe: deleting a rule cascades over its log rows. The
        // record is materialized client-side — EF cannot order over its members.
        var rows = await db.CaptureAlertLogs.AsNoTracking()
            .Where(l => l.CaptureId == captureId)
            .Join(db.CaptureRules, l => l.CaptureRuleId, r => r.Id,
                (l, r) => new { Log = l, RuleName = r.Name })
            .OrderByDescending(x => x.Log.SentAt).ThenByDescending(x => x.Log.Id)
            .ToListAsync(ct);
        return rows.Select(x => new CaptureAlertLogEntry(x.Log, x.RuleName)).ToList();
    }
}
