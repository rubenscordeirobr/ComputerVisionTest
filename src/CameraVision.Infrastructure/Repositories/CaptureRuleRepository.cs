using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class CaptureRuleRepository(IDbContextFactory<AppDbContext> factory) : ICaptureRuleRepository
{
    public async Task<IReadOnlyList<CaptureRule>> GetAllAsync(int? tenantId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = WithTriggers(db);
        if (tenantId is { } tid)
            query = query.Where(r => r.TenantId == tid);
        return await query.OrderBy(r => r.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CaptureRule>> GetEnabledAsync(int? tenantId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = WithTriggers(db).Where(r => r.Enabled);
        if (tenantId is { } tid)
            query = query.Where(r => r.TenantId == tid);
        return await query.ToListAsync(ct);
    }

    public async Task<CaptureRule?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await WithTriggers(db).FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CaptureRules.AnyAsync(ct);
    }

    public async Task AddAsync(CaptureRule rule, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CaptureRules.Add(rule); // attached triggers are inserted along
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CaptureRule rule, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.CaptureRules.FirstOrDefaultAsync(r => r.Id == rule.Id, ct);
        if (existing == null)
            return;
        // Scalars only — the navigation is ignored, so a caller holding a rule loaded
        // without (or with stale) triggers cannot wipe them.
        db.Entry(existing).CurrentValues.SetValues(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.CaptureRules.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task ReplaceTriggersAsync(int ruleId, IReadOnlyList<AlertTrigger> triggers, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.AlertTriggers.Where(t => t.CaptureRuleId == ruleId).ToListAsync(ct);
        var keptIds = triggers.Where(t => t.Id != 0).Select(t => t.Id).ToHashSet();

        foreach (var stale in existing.Where(t => !keptIds.Contains(t.Id)))
            db.AlertTriggers.Remove(stale);

        foreach (var trigger in triggers)
        {
            trigger.CaptureRuleId = ruleId;
            var current = trigger.Id == 0 ? null : existing.FirstOrDefault(t => t.Id == trigger.Id);
            if (current == null)
            {
                trigger.Id = 0; // unknown id (deleted meanwhile) → insert as new
                db.AlertTriggers.Add(trigger);
            }
            else
            {
                db.Entry(current).CurrentValues.SetValues(trigger);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task SaveTriggerAsync(AlertTrigger trigger, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (trigger.Id == 0)
        {
            db.AlertTriggers.Add(trigger);
        }
        else
        {
            var existing = await db.AlertTriggers.FirstOrDefaultAsync(t => t.Id == trigger.Id, ct);
            if (existing == null)
                return;
            db.Entry(existing).CurrentValues.SetValues(trigger);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteTriggersAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return;
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.AlertTriggers.Where(t => idList.Contains(t.Id)).ExecuteDeleteAsync(ct);
    }

    private static IQueryable<CaptureRule> WithTriggers(AppDbContext db) =>
        db.CaptureRules.AsNoTracking().Include(r => r.Triggers.OrderBy(t => t.Id));
}
