using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ICaptureRuleRepository
{
    Task<IReadOnlyList<CaptureRule>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CaptureRule>> GetEnabledAsync(CancellationToken ct = default);
    Task<CaptureRule?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task AddAsync(CaptureRule rule, CancellationToken ct = default);
    Task UpdateAsync(CaptureRule rule, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
