using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class AgentSuggestionRepository(IDbContextFactory<AppDbContext> factory) : IAgentSuggestionRepository
{
    public async Task AddAsync(AgentSuggestion suggestion, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.AgentSuggestions.Add(suggestion);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AgentSuggestion>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AgentSuggestions.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountNewAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AgentSuggestions.CountAsync(s => s.ReviewedAt == null, ct);
    }

    public async Task MarkReviewedAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTime.Now;
        await db.AgentSuggestions
            .Where(s => s.Id == id && s.ReviewedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReviewedAt, now), ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.AgentSuggestions.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
    }
}
