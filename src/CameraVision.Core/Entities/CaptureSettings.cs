namespace CameraVision.Core.Entities;

/// <summary>Singleton row (Id = 1). Defaults mirror the pipeline's appsettings.json.</summary>
public class CaptureSettings
{
    public int Id { get; set; } = 1;
    public List<string> TrackedClasses { get; set; } = ["person"];
    public int MaxSegmentSeconds { get; set; } = 60;
    public double LingerSeconds { get; set; } = 2.0;
    public double ConfidenceThreshold { get; set; } = 0.5;
}
