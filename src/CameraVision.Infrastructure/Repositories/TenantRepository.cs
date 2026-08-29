using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class TenantRepository(IDbContextFactory<AppDbContext> factory) : ITenantRepository
{
    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Tenants.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TenantSummary>> GetSummariesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var tenants = await db.Tenants.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        var userCounts = await db.Users.AsNoTracking()
            .Where(u => u.TenantId != null)
            .GroupBy(u => u.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);
        var cameraCounts = await db.Cameras.AsNoTracking()
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        return tenants
            .Select(t => new TenantSummary(t,
                userCounts.GetValueOrDefault(t.Id),
                cameraCounts.GetValueOrDefault(t.Id)))
            .ToList();
    }

    public async Task<Tenant?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Tenant?> GetDefaultAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Tenants.AsNoTracking().OrderBy(t => t.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Tenants.AnyAsync(ct);
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Tenants.AnyAsync(
            t => t.Name.ToLower() == name.ToLower() && (excludeId == null || t.Id != excludeId), ct);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Tenants.Update(tenant);
        await db.SaveChangesAsync(ct);
    }
}
