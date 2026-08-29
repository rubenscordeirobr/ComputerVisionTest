using System.Text.Json;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure;

/// <summary>
/// One-time best-effort import of the pipeline's data/cameras.json into the Cameras
/// table, so a fresh database starts populated. Runs only while the table is empty;
/// afterwards the database and the JSON file are independent.
/// </summary>
public static class LegacyCameraImporter
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public static async Task ImportIfEmptyAsync(
        ICameraRepository cameras, ITenantRepository tenants, string camerasJsonPath,
        ILogger logger, CancellationToken ct = default)
    {
        try
        {
            if (await cameras.AnyAsync(ct) || !File.Exists(camerasJsonPath))
                return;

            var defaultTenantId = (await tenants.GetDefaultAsync(ct))?.Id;
            if (defaultTenantId == null)
                return;

            var entries = await ReadEntriesAsync(camerasJsonPath, ct);

            var imported = 0;
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;
                await cameras.AddAsync(new Camera
                {
                    TenantId = defaultTenantId.Value,
                    Name = entry.Name.Trim(),
                    StreamUrl = entry.RtspUrl?.Trim() ?? "",
                    SubStreamUrl = string.IsNullOrWhiteSpace(entry.SubRtspUrl) ? null : entry.SubRtspUrl.Trim(),
                    PreferredStream = entry.Stream?.Trim().ToLowerInvariant() == "sub" ? "sub" : "main",
                    IpAddress = string.IsNullOrWhiteSpace(entry.IpAddress) ? null : entry.IpAddress.Trim(),
                    Enabled = entry.Enabled ?? true,
                }, ct);
                imported++;
            }

            if (imported > 0)
                logger.LogInformation("Imported {Count} camera(s) from {Path}.", imported, camerasJsonPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Legacy camera import failed (non-fatal).");
        }
    }

    /// <summary>
    /// One-time backfill for databases created before the substream fields existed:
    /// cameras matched by name get SubStreamUrl/PreferredStream (and IP) from the JSON.
    /// </summary>
    public static async Task EnrichFromLegacyAsync(
        ICameraRepository cameras, string camerasJsonPath, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(camerasJsonPath))
                return;
            var entries = await ReadEntriesAsync(camerasJsonPath, ct);

            var enriched = 0;
            foreach (var camera in await cameras.GetAllAsync(ct: ct))
            {
                if (camera.SubStreamUrl != null)
                    continue;
                var entry = entries.FirstOrDefault(e =>
                    string.Equals(e.Name?.Trim(), camera.Name, StringComparison.OrdinalIgnoreCase));
                if (entry == null || string.IsNullOrWhiteSpace(entry.SubRtspUrl))
                    continue;

                camera.SubStreamUrl = entry.SubRtspUrl.Trim();
                camera.PreferredStream = entry.Stream?.Trim().ToLowerInvariant() == "sub" ? "sub" : "main";
                camera.IpAddress ??= string.IsNullOrWhiteSpace(entry.IpAddress) ? null : entry.IpAddress.Trim();
                await cameras.UpdateAsync(camera, ct);
                enriched++;
            }

            if (enriched > 0)
                logger.LogInformation("Enriched {Count} camera(s) with substream data from {Path}.",
                    enriched, camerasJsonPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Legacy camera enrichment failed (non-fatal).");
        }
    }

    private static async Task<List<LegacyCamera>> ReadEntriesAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<LegacyCamera>>(stream, _json, ct) ?? [];
    }

    private sealed record LegacyCamera(
        string? Name, string? RtspUrl, string? SubRtspUrl, string? Stream,
        string? IpAddress, bool? Enabled);
}
