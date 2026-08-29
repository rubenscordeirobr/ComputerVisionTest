namespace CameraVision.Core.Entities;

public enum AlertChannel
{
    Email,
    WhatsApp,
}

/// <summary>
/// Per-tenant, per-channel delivery configuration (one row per tenant+channel):
/// master switch + recipients. What triggers an alert is decided by the capture
/// rules (CaptureRule) and the health-alert settings — not here.
/// </summary>
public class AlertSettings
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public AlertChannel Channel { get; set; }
    public bool Enabled { get; set; }

    /// <summary>Email addresses or phone numbers, depending on the channel.</summary>
    public List<string> Recipients { get; set; } = [];
}
