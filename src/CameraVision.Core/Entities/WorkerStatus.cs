namespace CameraVision.Core.Entities;

/// <summary>
/// Singleton row (Id = 1): the DetectionWorker's global heartbeat, posted every
/// ~30 s independently of per-camera status so liveness is known even when no
/// camera is streaming. Older workers only report per-camera status — readers
/// fall back to the newest Camera.ProcessorStatusAt.
/// </summary>
public class WorkerStatus
{
    public int Id { get; set; } = 1;

    public DateTime LastHeartbeatAt { get; set; }

    public DateTime? StartedAt { get; set; }

    /// <summary>Inference device description (e.g. "CUDA (GPU 0)" or "CPU").</summary>
    public string? Device { get; set; }

    public int ActiveCameras { get; set; }
}
