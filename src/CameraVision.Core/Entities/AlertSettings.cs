namespace CameraVision.Core.Entities;

public enum AlertChannel
{
    Email,
    WhatsApp,
}

/// <summary>
/// Per-tenant, per-channel master switch (one row per tenant+channel). Who receives
/// what is decided elsewhere: capture-rule notifications pick contacts (AlertTrigger)
/// and camera-health alerts go to the contacts flagged for them (Contact).
/// </summary>
public class AlertSettings
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public AlertChannel Channel { get; set; }
    public bool Enabled { get; set; }
}
