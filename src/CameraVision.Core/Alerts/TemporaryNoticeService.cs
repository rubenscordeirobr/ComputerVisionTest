using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;

namespace CameraVision.Core.Alerts;

/// <summary>Outcome of activating a notice for one contact.</summary>
public sealed record TemporaryNoticeResult(int Created, int Extended, DateTime? ExpiresAt)
{
    public int Rules => Created + Extended;
}

/// <summary>
/// Temporary notices ("Aviso temporário"): one Temporary trigger per rule, active from
/// now until ExpiresAt (null = until ended). Shared by the web dialog, the rules page
/// and the WhatsApp command agent, so every path creates and ends them the same way.
/// A notice the agent manages for a sender is a trigger whose only contact is that
/// sender; shared triggers created from the UI are never deleted on the sender's
/// behalf — the sender is just removed from them.
/// </summary>
public sealed class TemporaryNoticeService(ICaptureRuleRepository rules)
{
    public static readonly TimeSpan MinDuration = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(72);

    /// <summary>Keeps a requested end between 1 h and 72 h from now; null (until ended) passes through.</summary>
    public static DateTime? Clamp(DateTime now, DateTime? until)
    {
        if (until == null)
            return null;
        var min = now + MinDuration;
        var max = now + MaxDuration;
        return until < min ? min : until > max ? max : until;
    }

    /// <summary>The dialog: one notice per selected rule for the chosen contacts. Returns the rule count.</summary>
    public async Task<int> ActivateAsync(IReadOnlyCollection<int> ruleIds, AlertChannel channel,
        IReadOnlyList<int> contactIds, DateTime now, DateTime? expiresAt, CancellationToken ct = default)
    {
        foreach (var ruleId in ruleIds)
        {
            await rules.SaveTriggerAsync(new AlertTrigger
            {
                CaptureRuleId = ruleId,
                Channel = channel,
                ContactIds = contactIds.ToList(),
                Kind = AlertTriggerKind.Temporary,
                Days = DaysOfWeek.All,
                ActiveFrom = now,
                ExpiresAt = expiresAt,
            }, ct);
        }
        return ruleIds.Count;
    }

    /// <summary>
    /// The agent: a notice on every enabled rule of the tenant for one contact. A rule
    /// that already has the contact's own running notice gets its end moved instead of
    /// a second trigger, so repeating "ativar" never doubles messages.
    /// </summary>
    public async Task<TemporaryNoticeResult> ActivateForContactAsync(int tenantId, int contactId, AlertChannel channel,
        DateTime now, DateTime? expiresAt, CancellationToken ct = default)
    {
        expiresAt = Clamp(now, expiresAt);
        var created = 0;
        var extended = 0;
        foreach (var rule in await rules.GetEnabledAsync(tenantId, ct))
        {
            var own = OwnRunningNotice(rule, contactId, channel, now);
            if (own != null)
            {
                own.ExpiresAt = expiresAt;
                await rules.SaveTriggerAsync(own, ct);
                extended++;
                continue;
            }

            await rules.SaveTriggerAsync(new AlertTrigger
            {
                CaptureRuleId = rule.Id,
                Channel = channel,
                ContactIds = [contactId],
                Kind = AlertTriggerKind.Temporary,
                Days = DaysOfWeek.All,
                ActiveFrom = now,
                ExpiresAt = expiresAt,
            }, ct);
            created++;
        }
        return new TemporaryNoticeResult(created, extended, expiresAt);
    }

    /// <summary>Moves the end of the contact's own running notices (the answer to "até quando?"). Returns the rule count.</summary>
    public async Task<int> SetExpiryForContactAsync(int tenantId, int contactId, AlertChannel channel,
        DateTime now, DateTime? expiresAt, CancellationToken ct = default)
    {
        expiresAt = Clamp(now, expiresAt);
        var updated = 0;
        foreach (var rule in await rules.GetAllAsync(tenantId, ct))
        {
            var own = OwnRunningNotice(rule, contactId, channel, now);
            if (own == null)
                continue;
            own.ExpiresAt = expiresAt;
            await rules.SaveTriggerAsync(own, ct);
            updated++;
        }
        return updated;
    }

    /// <summary>Ends the contact's running notices on the channel. Returns the rule count.</summary>
    public async Task<int> EndForContactAsync(int tenantId, int contactId, AlertChannel channel, DateTime now,
        CancellationToken ct = default)
    {
        var toDelete = new List<int>();
        var ended = 0;
        foreach (var rule in await rules.GetAllAsync(tenantId, ct))
        {
            foreach (var trigger in rule.Triggers.Where(t =>
                         t.Channel == channel && t.IsRunningTemporaryAt(now) && t.ContactIds.Contains(contactId)))
            {
                if (trigger.ContactIds.Count == 1)
                {
                    toDelete.Add(trigger.Id);
                }
                else
                {
                    trigger.ContactIds = trigger.ContactIds.Where(id => id != contactId).ToList();
                    await rules.SaveTriggerAsync(trigger, ct);
                }
                ended++;
            }
        }
        if (toDelete.Count > 0)
            await rules.DeleteTriggersAsync(toDelete, ct);
        return ended;
    }

    /// <summary>The rules page banner: deletes every running notice of the given rules.</summary>
    public async Task<int> EndAllRunningAsync(IEnumerable<CaptureRule> captureRules, DateTime now,
        CancellationToken ct = default)
    {
        var ids = captureRules
            .SelectMany(r => r.Triggers)
            .Where(t => t.IsRunningTemporaryAt(now))
            .Select(t => t.Id)
            .ToList();
        if (ids.Count > 0)
            await rules.DeleteTriggersAsync(ids, ct);
        return ids.Count;
    }

    private static AlertTrigger? OwnRunningNotice(CaptureRule rule, int contactId, AlertChannel channel, DateTime now) =>
        rule.Triggers.FirstOrDefault(t =>
            t.Channel == channel && t.IsRunningTemporaryAt(now) &&
            t.ContactIds.Count == 1 && t.ContactIds[0] == contactId);
}
