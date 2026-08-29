namespace CameraVision.Core.Entities;

public enum AlertChannel
{
    Email,
    WhatsApp,
}

/// <summary>Per-channel alert configuration (one row per channel).</summary>
public class AlertSettings
{
    public int Id { get; set; }
    public AlertChannel Channel { get; set; }
    public bool Enabled { get; set; }

    /// <summary>Email addresses or phone numbers, depending on the channel.</summary>
    public List<string> Recipients { get; set; } = [];

    /// <summary>English COCO class names that trigger alerts.</summary>
    public List<string> TriggerClasses { get; set; } = [];
}
