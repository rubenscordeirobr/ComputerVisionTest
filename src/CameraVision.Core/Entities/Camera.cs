namespace CameraVision.Core.Entities;

public class Camera
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Empty for cameras auto-created by the capture import.</summary>
    public string StreamUrl { get; set; } = "";

    public string? IpAddress { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
