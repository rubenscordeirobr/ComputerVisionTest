using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ISystemAlertEventRepository
{
    Task AddAsync(SystemAlertEvent alertEvent, CancellationToken ct = default);

    Task<IReadOnlyList<SystemAlertEvent>> GetRecentAsync(int take, CancellationToken ct = default);

    /// <summary>Most recent event regardless of type (worker up/down reconciliation on startup).</summary>
    Task<SystemAlertEvent?> GetLastAsync(CancellationToken ct = default);

    Task<DateTime?> GetLastNotifiedAtAsync(SystemAlertType type, CancellationToken ct = default);
}
