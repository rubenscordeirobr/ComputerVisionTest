namespace CameraVision.Core.Entities;

public enum AlertDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}

/// <summary>
/// Outbox row and delivery log in one: the dispatcher queues one row per capture ×
/// rule × channel × recipient, the web app's delivery service sends it and records
/// the outcome, and the row then stays as history (the capture's "Notificações" dialog).
/// </summary>
public class AlertDelivery
{
    public int Id { get; set; }

    /// <summary>Copied from the capture.</summary>
    public int TenantId { get; set; }

    public int CaptureId { get; set; }
    public int CaptureRuleId { get; set; }
    public AlertChannel Channel { get; set; }

    /// <summary>Contact the recipient came from; null once that contact is deleted, and on migrated rows.</summary>
    public int? ContactId { get; set; }

    /// <summary>Normalized address resolved at queue time. Null only on rows migrated from the old alert log.</summary>
    public string? Recipient { get; set; }

    public DateTime QueuedAt { get; set; }

    /// <summary>Time of the delivery attempt — set for Sent AND Failed; it drives the rule's grouping window.</summary>
    public DateTime? SentAt { get; set; }

    public AlertDeliveryStatus Status { get; set; }

    /// <summary>PT-BR reason when Failed.</summary>
    public string? ErrorMessage { get; set; }
}
