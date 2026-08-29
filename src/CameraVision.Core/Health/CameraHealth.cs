namespace CameraVision.Core.Health;

public enum CameraStatus
{
    Unknown,
    Online,
    Offline,
    Disabled,
}

/// <summary>Latest probe result for one camera (in-memory only).</summary>
public sealed record CameraHealth(
    int CameraId,
    CameraStatus Status,
    long? PingMs,
    long? ConnectMs,
    DateTime CheckedAt);

public interface ICameraHealthService
{
    CameraHealth? TryGet(int cameraId);
    event Action? Changed;
}
