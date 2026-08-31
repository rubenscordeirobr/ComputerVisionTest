using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface IWorkerStatusRepository
{
    /// <summary>Null when the worker never sent a global heartbeat.</summary>
    Task<WorkerStatus?> GetAsync(CancellationToken ct = default);

    Task SaveHeartbeatAsync(WorkerStatus status, CancellationToken ct = default);
}
