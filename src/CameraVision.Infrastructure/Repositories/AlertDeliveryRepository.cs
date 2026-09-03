using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class AlertDeliveryRepository(IDbContextFactory<AppDbContext> factory) : IAlertDeliveryRepository
{
    public async Task AddRangeAsync(IEnumerable<AlertDelivery> deliveries, CancellationToken ct = default)
    {
        var rows = deliveries.ToList();
        if (rows.Count == 0)
            return;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.AlertDeliveries.AddRange(rows);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PendingDelivery>> GetPendingAsync(int take, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // Inner joins are safe: deleting a capture or a rule cascades over its rows.
        // The record is materialized client-side — EF cannot order over its members.
        var rows = await db.AlertDeliveries.AsNoTracking()
            .Where(d => d.Status == AlertDeliveryStatus.Pending)
            .Join(db.Captures, d => d.CaptureId, c => c.Id, (d, c) => new { Delivery = d, Capture = c })
            .Join(db.CaptureRules, x => x.Delivery.CaptureRuleId, r => r.Id,
                (x, r) => new { x.Delivery, x.Capture, Rule = r })
            .OrderBy(x => x.Delivery.QueuedAt).ThenBy(x => x.Delivery.Id)
            .Take(take)
            .ToListAsync(ct);
        return rows.Select(x => new PendingDelivery(x.Delivery, x.Capture, x.Rule)).ToList();
    }

    public async Task<IReadOnlyDictionary<int, DateTime>> GetLastAttemptByRuleAsync(IEnumerable<int> ruleIds, CancellationToken ct = default)
    {
        var ids = ruleIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, DateTime>();
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.AlertDeliveries.AsNoTracking()
            .Where(d => ids.Contains(d.CaptureRuleId) && d.SentAt != null)
            .GroupBy(d => d.CaptureRuleId)
            .Select(g => new { RuleId = g.Key, Last = g.Max(d => d.SentAt) })
            .ToListAsync(ct);
        return rows.Where(r => r.Last != null).ToDictionary(r => r.RuleId, r => r.Last!.Value);
    }

    public async Task MarkAsync(IEnumerable<AlertDeliveryOutcome> outcomes, DateTime attemptedAt, CancellationToken ct = default)
    {
        var groups = outcomes
            .GroupBy(o => (o.Status, o.ErrorMessage))
            .Select(g => (g.Key.Status, g.Key.ErrorMessage, Ids: g.Select(o => o.DeliveryId).Distinct().ToList()))
            .ToList();
        if (groups.Count == 0)
            return;

        await using var db = await factory.CreateDbContextAsync(ct);
        foreach (var (status, error, ids) in groups)
        {
            await db.AlertDeliveries
                .Where(d => ids.Contains(d.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, status)
                    .SetProperty(d => d.ErrorMessage, error)
                    .SetProperty(d => d.SentAt, attemptedAt), ct);
        }
    }

    public async Task<IReadOnlyList<AlertDeliveryEntry>> GetByCaptureAsync(int captureId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (
                from d in db.AlertDeliveries.AsNoTracking()
                where d.CaptureId == captureId
                join r in db.CaptureRules on d.CaptureRuleId equals r.Id
                join c in db.Contacts on d.ContactId equals (int?)c.Id into contacts
                from c in contacts.DefaultIfEmpty()
                orderby d.QueuedAt descending, d.Id descending
                select new { Delivery = d, RuleName = r.Name, ContactName = c != null ? c.Name : null })
            .ToListAsync(ct);
        return rows.Select(x => new AlertDeliveryEntry(x.Delivery, x.RuleName, x.ContactName)).ToList();
    }
}
