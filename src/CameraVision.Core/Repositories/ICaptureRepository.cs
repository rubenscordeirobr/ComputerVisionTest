using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ICaptureRepository
{
    Task<CapturePage> QueryAsync(CaptureFilter filter, CancellationToken ct = default);
    Task<Capture?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Capture?> GetByFilePathAsync(string filePath, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Capture> captures, CancellationToken ct = default);
    Task UpdateAsync(Capture capture, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetKnownFilePathsAsync(CancellationToken ct = default);
    Task<int> RemoveByFilePathsAsync(IEnumerable<string> filePaths, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctCameraNamesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctClassesAsync(CancellationToken ct = default);
    Task<int> CountAsync(DateTime? startedAtOrAfter = null, CancellationToken ct = default);

    /// <summary>Captures queued for the grouped alert summary, oldest first.</summary>
    Task<IReadOnlyList<Capture>> GetPendingAlertsAsync(int take, CancellationToken ct = default);

    Task MarkAlertsSentAsync(IEnumerable<int> ids, DateTime sentAt, CancellationToken ct = default);
}
