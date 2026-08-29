using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ICaptureRuleRepository
{
    /// <summary>Rules of one tenant, or every tenant's when <paramref name="tenantId"/> is null.</summary>
    Task<IReadOnlyList<CaptureRule>> GetAllAsync(int? tenantId = null, CancellationToken ct = default);

    /// <summary>Enabled rules of one tenant; null = union of all tenants (worker endpoint).</summary>
    Task<IReadOnlyList<CaptureRule>> GetEnabledAsync(int? tenantId = null, CancellationToken ct = default);

    Task<CaptureRule?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task AddAsync(CaptureRule rule, CancellationToken ct = default);
    Task UpdateAsync(CaptureRule rule, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
