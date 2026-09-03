using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Alerts;

/// <summary>
/// Evaluates the capture rules for freshly imported captures and queues one
/// AlertDelivery per resolved recipient (see AlertTargetResolver). Nothing is sent
/// here: the web app's delivery service sends, applying each rule's grouping window.
/// Captures older than the recency window never alert, so importing a historical
/// backlog stays silent. Each capture is only ever seen here once (the import/ingest
/// is insert-once).
/// </summary>
public sealed class AlertDispatcher(
    ICaptureRuleRepository ruleRepository,
    IContactRepository contactRepository,
    IAlertDeliveryRepository deliveryRepository,
    ILogger<AlertDispatcher> logger) : IAlertDispatcher
{
    private static readonly TimeSpan RecencyWindow = TimeSpan.FromMinutes(15);

    public async Task DispatchAsync(IReadOnlyList<Capture> newCaptures, CancellationToken ct = default)
    {
        try
        {
            await DispatchCoreAsync(newCaptures, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Alert dispatch failed.");
        }
    }

    private async Task DispatchCoreAsync(IReadOnlyList<Capture> newCaptures, CancellationToken ct)
    {
        if (newCaptures.Count == 0)
            return;

        var recent = newCaptures.Where(c => DateTime.Now - c.EndedAt <= RecencyWindow).ToList();
        if (recent.Count == 0)
            return;

        // Rules and contacts are tenant-scoped: a capture only matches its own
        // tenant's rules and contacts (SPEC-14).
        foreach (var tenantCaptures in recent.GroupBy(c => c.TenantId))
            await DispatchTenantAsync(tenantCaptures.Key, [.. tenantCaptures], ct);
    }

    private async Task DispatchTenantAsync(int tenantId, IReadOnlyList<Capture> tenantCaptures, CancellationToken ct)
    {
        var rules = await ruleRepository.GetEnabledAsync(tenantId, ct);
        if (rules.Count == 0)
            return;
        var contacts = (await contactRepository.GetAllAsync(tenantId, ct)).ToDictionary(c => c.Id);

        foreach (var capture in tenantCaptures)
        {
            var targets = AlertTargetResolver.Resolve(capture, rules, contacts);
            if (targets.Count == 0)
            {
                var unresolved = AlertTargetResolver.MatchingRules(capture, rules)
                    .Where(r => r.Triggers.Any(t => t.IsActiveAt(capture.StartedAt)))
                    .Select(r => r.Name)
                    .ToList();
                if (unresolved.Count > 0)
                    logger.LogWarning(
                        "Capture {CaptureId} ({Class} @ {Camera}) matched rule(s) {Rules} but no contact could be resolved — check the contacts' addresses.",
                        capture.Id, capture.ObjectClass, capture.CameraName, string.Join(", ", unresolved));
                continue;
            }

            var now = DateTime.Now;
            var deliveries = targets.Select(t => new AlertDelivery
            {
                TenantId = capture.TenantId,
                CaptureId = capture.Id,
                CaptureRuleId = t.Rule.Id,
                Channel = t.Channel,
                ContactId = t.Contact.Id,
                Recipient = t.Recipient,
                QueuedAt = now,
                Status = AlertDeliveryStatus.Pending,
            }).ToList();

            await deliveryRepository.AddRangeAsync(deliveries, ct);
            logger.LogInformation(
                "Queued {Count} notification(s) for capture {CaptureId} ({Class} @ {Camera}).",
                deliveries.Count, capture.Id, capture.ObjectClass, capture.CameraName);
        }
    }
}
