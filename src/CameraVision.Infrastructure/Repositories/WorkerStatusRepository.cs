using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class WorkerStatusRepository(IDbContextFactory<AppDbContext> factory) : IWorkerStatusRepository
{
    public async Task<WorkerStatus?> GetAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.WorkerStatus.AsNoTracking().FirstOrDefaultAsync(ct);
    }

    public async Task SaveHeartbeatAsync(WorkerStatus status, CancellationToken ct = default)
    {
        status.Id = 1;
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.WorkerStatus.FirstOrDefaultAsync(ct);
        if (existing == null)
            db.Add(status);
        else
            db.Entry(existing).CurrentValues.SetValues(status);
        await db.SaveChangesAsync(ct);
    }
}
