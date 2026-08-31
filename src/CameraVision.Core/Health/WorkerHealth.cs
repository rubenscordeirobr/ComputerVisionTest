namespace CameraVision.Core.Health;

/// <summary>
/// Staleness rules for the DetectionWorker. The worker reports each camera's
/// status and a global heartbeat every ~30 s; anything older than StaleAfter
/// means "the worker stopped updating" — a state that is neither online nor
/// offline and gets its own label in the UI.
/// </summary>
public static class WorkerHealth
{
    /// <summary>Worker reporting cadence (api.statusIntervalSeconds on the worker side).</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    /// <summary>One heartbeat plus grace — older reports count as stale.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(35);

    public static bool IsStale(DateTime? reportedAt, DateTime now) =>
        reportedAt == null || now - reportedAt.Value > StaleAfter;
}

/// <summary>Aggregated DetectionWorker liveness, recomputed by the web monitor.</summary>
public sealed record WorkerHealthSnapshot(
    DateTime? LastSeenAt,
    DateTime? StartedAt,
    string? Device,
    int? ActiveCameras,
    bool HasProcessableCameras,
    DateTime CheckedAt)
{
    public bool EverSeen => LastSeenAt != null;

    /// <summary>The worker stopped updating (or never connected while cameras exist).</summary>
    public bool IsStale =>
        LastSeenAt != null ? WorkerHealth.IsStale(LastSeenAt, CheckedAt) : HasProcessableCameras;
}

public interface IWorkerHealthService
{
    WorkerHealthSnapshot? Current { get; }
    event Action? Changed;
}
