using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class UserRepository(IDbContextFactory<AppDbContext> factory) : IUserRepository
{
    public async Task<IReadOnlyList<AppUser>> GetAllAsync(int? tenantId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Users.AsNoTracking();
        if (tenantId is { } tid)
            query = query.Where(u => u.TenantId == tid);
        return await query.OrderBy(u => u.Username).ToListAsync(ct);
    }

    public async Task<AppUser?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), ct);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AnyAsync(ct);
    }

    public async Task<bool> UsernameExistsAsync(string username, int? excludeId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AnyAsync(
            u => u.Username.ToLower() == username.ToLower() && (excludeId == null || u.Id != excludeId), ct);
    }

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }
}
