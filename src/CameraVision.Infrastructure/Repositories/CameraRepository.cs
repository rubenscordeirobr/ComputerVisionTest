using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class CameraRepository(IDbContextFactory<AppDbContext> factory) : ICameraRepository
{
    public async Task<IReadOnlyList<Camera>> GetAllAsync(int? tenantId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Cameras.AsNoTracking();
        if (tenantId is { } tid)
            query = query.Where(c => c.TenantId == tid);
        return await query.OrderBy(c => c.Name).ToListAsync(ct);
    }

    public async Task<Camera?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Cameras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Camera?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Cameras.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), ct);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Cameras.AnyAsync(ct);
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Cameras.AnyAsync(
            c => c.Name.ToLower() == name.ToLower() && (excludeId == null || c.Id != excludeId), ct);
    }

    public async Task AddAsync(Camera camera, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Cameras.Add(camera);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Camera camera, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Cameras.Update(camera);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Cameras.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
    }
}
