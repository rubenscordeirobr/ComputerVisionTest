using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ICameraRepository
{
    /// <summary>Cameras of one tenant, or every tenant's when <paramref name="tenantId"/> is null.</summary>
    Task<IReadOnlyList<Camera>> GetAllAsync(int? tenantId = null, CancellationToken ct = default);

    Task<Camera?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Camera?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Camera camera, CancellationToken ct = default);
    Task UpdateAsync(Camera camera, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
