namespace CameraVision.Core.Entities;

/// <summary>
/// One user-defined capture rule: which object classes to record, who is notified
/// and when (Triggers), and how notifications are grouped (GroupWindowMinutes).
/// Multiple rules may coexist (e.g. "cat → e-mail always", "person → WhatsApp at night").
/// </summary>
public class CaptureRule
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>English COCO class names this rule records.</summary>
    public List<string> Classes { get; set; } = [];

    /// <summary>
    /// Optional annotation color per class ("#RRGGBB", keyed by COCO name). Classes
    /// missing here are drawn with the worker's default palette. The worker only
    /// annotates classes that appear in at least one enabled rule (SPEC-21).
    /// </summary>
    public Dictionary<string, string> ClassColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public double ConfidenceThreshold { get; set; } = 0.5;
    public int MaxSegmentSeconds { get; set; } = 60;
    public double LingerSeconds { get; set; } = 2.0;

    /// <summary>
    /// Antiflood: 0 = every capture is its own message; N &gt; 0 = each recipient gets at
    /// most one summary of this rule's captures per N minutes.
    /// </summary>
    public int GroupWindowMinutes { get; set; } = 3;

    /// <summary>
    /// Optional time-of-day window. Both null = always active; otherwise the rule
    /// applies in [ActiveFrom, ActiveTo), wrapping midnight when ActiveTo &lt;= ActiveFrom
    /// (e.g. 22:00–06:00). "00:00 até 06:00" captures only during the night.
    /// Gates recording (worker) and alerting alike.
    /// </summary>
    public TimeOnly? ActiveFrom { get; set; }

    public TimeOnly? ActiveTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Notifications of the rule (loaded with it).</summary>
    public List<AlertTrigger> Triggers { get; set; } = [];

    public bool IsAlwaysActive => ActiveFrom == null || ActiveTo == null;

    /// <summary>The configured color of <paramref name="className"/>, or null for the default.</summary>
    public string? ColorFor(string className) =>
        ClassColors.TryGetValue(className, out var hex) ? hex : null;

    public bool IsActiveAt(TimeOnly time)
    {
        if (ActiveFrom is not { } from || ActiveTo is not { } to)
            return true;
        return from < to
            ? time >= from && time < to
            : time >= from || time < to; // window crosses midnight
    }
}
