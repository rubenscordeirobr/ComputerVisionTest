namespace CameraVision.Core.Entities;

public class Camera
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Globally unique (names map to shared output folders), see SPEC-14.</summary>
    public string Name { get; set; } = "";

    /// <summary>Empty for cameras auto-created by the capture import.</summary>
    public string StreamUrl { get; set; } = "";

    /// <summary>Optional low-bitrate substream URL.</summary>
    public string? SubStreamUrl { get; set; }

    /// <summary>"main" (default) or "sub" — which stream the DetectionWorker consumes.</summary>
    public string PreferredStream { get; set; } = "main";

    public string? IpAddress { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Last runtime state reported by the DetectionWorker (connected/reconnecting/stopped).</summary>
    public string? ProcessorStatus { get; set; }

    public DateTime? ProcessorStatusAt { get; set; }
}
