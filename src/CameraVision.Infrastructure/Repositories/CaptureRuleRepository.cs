using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class CaptureRuleRepository(IDbContextFactory<AppDbContext> factory) : ICaptureRuleRepository
{
    public async Task<IReadOnlyList<CaptureRule>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CaptureRules.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CaptureRule>> GetEnabledAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CaptureRules.AsNoTracking().Where(r => r.Enabled).ToListAsync(ct);
    }

    public async Task<CaptureRule?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CaptureRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CaptureRules.AnyAsync(ct);
    }

    public async Task AddAsync(CaptureRule rule, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CaptureRules.Add(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CaptureRule rule, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CaptureRules.Update(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.CaptureRules.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }
}
