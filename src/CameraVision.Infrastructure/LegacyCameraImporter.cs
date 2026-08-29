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
        ICameraRepository cameras, string camerasJsonPath, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            if (await cameras.AnyAsync(ct) || !File.Exists(camerasJsonPath))
                return;

            await using var stream = File.OpenRead(camerasJsonPath);
            var entries = await JsonSerializer.DeserializeAsync<List<LegacyCamera>>(stream, _json, ct) ?? [];

            var imported = 0;
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;
                await cameras.AddAsync(new Camera
                {
                    Name = entry.Name.Trim(),
                    StreamUrl = entry.RtspUrl?.Trim() ?? "",
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

    private sealed record LegacyCamera(string? Name, string? RtspUrl, string? IpAddress, bool? Enabled);
}
