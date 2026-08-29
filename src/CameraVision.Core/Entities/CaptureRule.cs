namespace CameraVision.Core.Entities;

/// <summary>
/// One user-defined capture rule: which object classes to record and which alert
/// channels to notify when a matching capture is created. Multiple rules may
/// coexist (e.g. "cat → e-mail", "person → WhatsApp").
/// </summary>
public class CaptureRule
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>English COCO class names this rule records.</summary>
    public List<string> Classes { get; set; } = [];

    public double ConfidenceThreshold { get; set; } = 0.5;
    public int MaxSegmentSeconds { get; set; } = 60;
    public double LingerSeconds { get; set; } = 2.0;

    public bool NotifyEmail { get; set; }
    public bool NotifyWhatsApp { get; set; }

    /// <summary>
    /// Optional time-of-day window. Both null = always active; otherwise the rule
    /// applies in [ActiveFrom, ActiveTo), wrapping midnight when ActiveTo &lt;= ActiveFrom
    /// (e.g. 22:00–06:00). "00:00 até 06:00" captures only during the night.
    /// </summary>
    public TimeOnly? ActiveFrom { get; set; }

    public TimeOnly? ActiveTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsAlwaysActive => ActiveFrom == null || ActiveTo == null;

    public bool IsActiveAt(TimeOnly time)
    {
        if (ActiveFrom is not { } from || ActiveTo is not { } to)
            return true;
        return from < to
            ? time >= from && time < to
            : time >= from || time < to; // window crosses midnight
    }
}
