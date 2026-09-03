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
    Task<IReadOnlyList<string>> GetDistinctCameraNamesAsync(int? tenantId = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctClassesAsync(int? tenantId = null, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? startedAtOrAfter = null, int? tenantId = null, CancellationToken ct = default);
}
