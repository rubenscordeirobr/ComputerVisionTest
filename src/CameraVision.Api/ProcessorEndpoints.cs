using System.Text.Json;
using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Api;

/// <summary>DetectionWorker-facing endpoints, guarded by the X-Api-Key header.</summary>
public static class ProcessorEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static void MapProcessorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/processor");
        group.AddEndpointFilter(RequireApiKeyAsync);

        group.MapGet("/cameras", async (ICameraRepository cameras, CancellationToken ct) =>
        {
            // The worker is tenant-agnostic: it processes every tenant's cameras.
            var list = await cameras.GetAllAsync(ct: ct);
            var dtos = list
                .Where(c => c.Enabled && !string.IsNullOrWhiteSpace(c.StreamUrl))
                .Select(c => new ProcessorCameraDto(c.Id, c.Name, c.StreamUrl, c.SubStreamUrl, c.PreferredStream))
                .ToList();
            return Results.Ok(dtos);
        });

        group.MapGet("/capture-rules", async (ICaptureRuleRepository rules, CancellationToken ct) =>
        {
            // Union of every tenant's enabled rules — the worker records the superset;
            // per-tenant correctness is enforced at alert dispatch (SPEC-14).
            var enabled = await rules.GetEnabledAsync(ct: ct);
            var dto = new CaptureRulesDto(
                enabled.Select(r => new WorkerRuleDto(
                    r.Classes, r.ConfidenceThreshold, r.ActiveFrom, r.ActiveTo)).ToList(),
                enabled.Count == 0 ? 60 : enabled.Max(r => r.MaxSegmentSeconds),
                enabled.Count == 0 ? 2.0 : enabled.Max(r => r.LingerSeconds));
            return Results.Ok(dto);
        });

        group.MapPost("/cameras/{id:int}/status",
            async (int id, CameraStatusDto dto, ICameraRepository cameras, CancellationToken ct) =>
            {
                var camera = await cameras.GetByIdAsync(id, ct);
                if (camera == null)
                    return Results.NotFound();
                camera.ProcessorStatus = dto.Status.Trim().ToLowerInvariant();
                camera.ProcessorStatusAt = DateTime.Now;
                await cameras.UpdateAsync(camera, ct);
                return Results.NoContent();
            });

        group.MapPost("/heartbeat",
            async (WorkerHeartbeatDto dto, IWorkerStatusRepository workerStatus, CancellationToken ct) =>
            {
                await workerStatus.SaveHeartbeatAsync(new WorkerStatus
                {
                    LastHeartbeatAt = DateTime.Now,
                    StartedAt = dto.StartedAt,
                    Device = dto.Device,
                    ActiveCameras = dto.ActiveCameras,
                }, ct);
                return Results.NoContent();
            });

        group.MapPost("/captures", IngestCaptureAsync);
    }

    private static async ValueTask<object?> RequireApiKeyAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = configuration["Api:ProcessorApiKey"];
        var provided = context.HttpContext.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(expected) || !string.Equals(provided, expected, StringComparison.Ordinal))
            return Results.Unauthorized();
        return await next(context);
    }

    private static async Task<IResult> IngestCaptureAsync(
        HttpRequest request,
        ICaptureRepository captures,
        ICameraRepository cameras,
        ITenantRepository tenants,
        IAlertDispatcher dispatcher,
        StoragePaths storage,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
            return Results.BadRequest("Expected multipart form data.");
        var form = await request.ReadFormAsync(ct);

        CaptureIngestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CaptureIngestDto>(form["capture"].ToString(), JsonOpts);
        }
        catch (JsonException)
        {
            return Results.BadRequest("Invalid capture JSON.");
        }
        if (dto == null || string.IsNullOrWhiteSpace(dto.FilePath) ||
            string.IsNullOrWhiteSpace(dto.CameraName) || string.IsNullOrWhiteSpace(dto.ObjectClass))
            return Results.BadRequest("Incomplete capture metadata.");

        var relPath = dto.FilePath.Replace('\\', '/').TrimStart('/');
        var videoPath = Path.Combine(storage.OutputRoot, relPath.Replace('/', Path.DirectorySeparatorChar));

        string? relThumbnail = null;
        var thumbnail = form.Files["thumbnail"];
        if (thumbnail is { Length: > 0 })
        {
            var thumbnailPath = Path.ChangeExtension(videoPath, ".jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
            await using var stream = File.Create(thumbnailPath);
            await thumbnail.CopyToAsync(stream, ct);
            relThumbnail = Path.ChangeExtension(relPath, ".jpg");
        }

        var existing = await captures.GetByFilePathAsync(relPath, ct);
        if (existing != null)
        {
            if (existing.ThumbnailPath == null && relThumbnail != null)
            {
                existing.ThumbnailPath = relThumbnail;
                await captures.UpdateAsync(existing, ct);
            }
            return Results.Ok(new { id = existing.Id });
        }

        // Footage belongs to the camera's tenant; unknown cameras fall back to the
        // default tenant so no capture is ever orphaned.
        var camera = dto.CameraId is { } cameraId ? await cameras.GetByIdAsync(cameraId, ct) : null;
        camera ??= await cameras.GetByNameAsync(dto.CameraName.Trim(), ct);
        var tenantId = camera?.TenantId ?? (await tenants.GetDefaultAsync(ct))?.Id;
        if (tenantId == null)
            return Results.Conflict("No tenant configured yet.");

        var capture = new Capture
        {
            TenantId = tenantId.Value,
            CameraId = camera?.Id,
            CameraName = dto.CameraName.Trim(),
            ObjectClass = dto.ObjectClass.Trim(),
            TrackId = dto.TrackId,
            StartedAt = dto.StartedAt,
            EndedAt = dto.EndedAt,
            FilePath = relPath,
            ThumbnailPath = relThumbnail,
            IsMerged = dto.IsMerged,
            FileSizeBytes = dto.FileSizeBytes > 0
                ? dto.FileSizeBytes
                : File.Exists(videoPath) ? new FileInfo(videoPath).Length : 0,
            IndexedAt = DateTime.Now,
        };

        try
        {
            await captures.AddRangeAsync([capture], ct);
        }
        catch (DbUpdateException)
        {
            // unique FilePath race with the reconciliation indexer — the row exists now
            var winner = await captures.GetByFilePathAsync(relPath, ct);
            return Results.Ok(new { id = winner?.Id ?? 0 });
        }

        logger.LogInformation("Capture ingested from worker: {Path} ({Class} @ {Camera}).",
            relPath, capture.ObjectClass, capture.CameraName);
        await dispatcher.DispatchAsync([capture], ct);
        return Results.Ok(new { id = capture.Id });
    }
}
