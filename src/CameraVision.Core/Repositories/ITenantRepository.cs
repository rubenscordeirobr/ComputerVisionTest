using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ITenantRepository
{
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TenantSummary>> GetSummariesAsync(CancellationToken ct = default);
    Task<Tenant?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>The seeded tenant (lowest id) — absorbs data with no resolvable tenant.</summary>
    Task<Tenant?> GetDefaultAsync(CancellationToken ct = default);

    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(Tenant tenant, CancellationToken ct = default);
}
