using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface IUserRepository
{
    /// <summary>Users of one tenant, or everyone (incl. system users) when <paramref name="tenantId"/> is null.</summary>
    Task<IReadOnlyList<AppUser>> GetAllAsync(int? tenantId = null, CancellationToken ct = default);

    Task<AppUser?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task UpdateAsync(AppUser user, CancellationToken ct = default);
}
