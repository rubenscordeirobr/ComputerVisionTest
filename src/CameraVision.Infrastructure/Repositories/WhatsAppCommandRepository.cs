using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class WhatsAppCommandRepository(IDbContextFactory<AppDbContext> factory) : IWhatsAppCommandRepository
{
    public async Task<bool> TryAddAsync(WhatsAppCommandLog log, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.WhatsAppCommandLogs.AnyAsync(l => l.MessageId == log.MessageId, ct))
            return false;
        db.WhatsAppCommandLogs.Add(log);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost the race against a webhook retry: the unique index kept the first copy.
            return false;
        }
    }

    public async Task<IReadOnlyList<WhatsAppCommandLog>> GetPendingAsync(int take, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.WhatsAppCommandLogs.AsNoTracking()
            .Where(l => l.Status == WhatsAppCommandStatus.Pending)
            .OrderBy(l => l.ReceivedAt).ThenBy(l => l.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(WhatsAppCommandLog log, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.WhatsAppCommandLogs.Update(log);
        await db.SaveChangesAsync(ct);
    }

    public async Task<WhatsAppCommandLog?> GetLastProcessedBySenderAsync(string senderNumber, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.WhatsAppCommandLogs.AsNoTracking()
            .Where(l => l.SenderNumber == senderNumber && l.Status != WhatsAppCommandStatus.Pending)
            .OrderByDescending(l => l.ReceivedAt).ThenByDescending(l => l.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> CountBySenderSinceAsync(string senderNumber, DateTime since, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.WhatsAppCommandLogs
            .CountAsync(l => l.SenderNumber == senderNumber && l.ReceivedAt >= since, ct);
    }

    public async Task<IReadOnlyList<WhatsAppCommandLog>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.WhatsAppCommandLogs.AsNoTracking()
            .OrderByDescending(l => l.ReceivedAt).ThenByDescending(l => l.Id)
            .Take(take)
            .ToListAsync(ct);
    }
}
