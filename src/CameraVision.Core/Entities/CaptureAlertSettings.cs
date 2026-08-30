namespace CameraVision.Core.Entities;

/// <summary>
/// Per-tenant (one row per tenant) antiflood grouping for capture alerts. When
/// enabled, individual capture messages are replaced by one grouped summary per
/// window (per channel), sent by the web app's digest job.
/// </summary>
public class CaptureAlertSettings
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public bool GroupingEnabled { get; set; } = true;

    /// <summary>Minimum minutes between grouped capture summaries.</summary>
    public int GroupWindowMinutes { get; set; } = 3;

    public DateTime? LastDigestAt { get; set; }
}
