using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

/// <summary>A pending delivery with the capture and rule it belongs to.</summary>
public sealed record PendingDelivery(AlertDelivery Delivery, Capture Capture, CaptureRule Rule);

public sealed record AlertDeliveryOutcome(int DeliveryId, AlertDeliveryStatus Status, string? ErrorMessage);

/// <summary>A delivery row for display, with the rule name and the contact's current name.</summary>
public sealed record AlertDeliveryEntry(AlertDelivery Delivery, string RuleName, string? ContactName);

public interface IAlertDeliveryRepository
{
    Task AddRangeAsync(IEnumerable<AlertDelivery> deliveries, CancellationToken ct = default);

    /// <summary>Pending rows of every tenant, oldest first, joined with their capture and rule.</summary>
    Task<IReadOnlyList<PendingDelivery>> GetPendingAsync(int take, CancellationToken ct = default);

    /// <summary>Latest attempt time (MAX(SentAt)) per rule; rules without attempts are absent.</summary>
    Task<IReadOnlyDictionary<int, DateTime>> GetLastAttemptByRuleAsync(IEnumerable<int> ruleIds, CancellationToken ct = default);

    /// <summary>Records the outcome of delivery attempts (SentAt = <paramref name="attemptedAt"/> for every row).</summary>
    Task MarkAsync(IEnumerable<AlertDeliveryOutcome> outcomes, DateTime attemptedAt, CancellationToken ct = default);

    /// <summary>Every row of one capture (pending included), newest first.</summary>
    Task<IReadOnlyList<AlertDeliveryEntry>> GetByCaptureAsync(int captureId, CancellationToken ct = default);
}
