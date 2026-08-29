using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ICameraHealthEventRepository
{
    Task AddAsync(CameraHealthEvent healthEvent, CancellationToken ct = default);
    Task<IReadOnlyList<CameraHealthEvent>> GetRecentByCameraAsync(int cameraId, int take, CancellationToken ct = default);
    Task<int> CountNotifiedSinceAsync(DateTime since, CancellationToken ct = default);
    Task<DateTime?> GetLastNotifiedAtAsync(string cameraName, HealthCondition condition, CancellationToken ct = default);
    Task<IReadOnlyList<CameraHealthEvent>> GetPendingForDigestAsync(int take, CancellationToken ct = default);
    Task MarkDigestedAsync(IEnumerable<int> ids, DateTime digestedAt, CancellationToken ct = default);
}
