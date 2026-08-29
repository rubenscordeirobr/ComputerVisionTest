using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Alerts;

/// <summary>
/// Evaluates the per-channel alert rules for freshly imported captures. Captures older
/// than the recency window are never alerted, so importing a historical backlog stays
/// silent. Each capture is only ever seen here once (the import is insert-once).
/// </summary>
public sealed class AlertDispatcher(
    IEnumerable<IAlertChannel> channels,
    ISettingsRepository settingsRepository,
    StoragePaths storage,
    ILogger<AlertDispatcher> logger) : IAlertDispatcher
{
    private static readonly TimeSpan RecencyWindow = TimeSpan.FromMinutes(15);

    public async Task DispatchAsync(IReadOnlyList<Capture> newCaptures, CancellationToken ct = default)
    {
        try
        {
            await DispatchCoreAsync(newCaptures, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Alert dispatch failed.");
        }
    }

    private async Task DispatchCoreAsync(IReadOnlyList<Capture> newCaptures, CancellationToken ct)
    {
        if (newCaptures.Count == 0)
            return;

        var recent = newCaptures.Where(c => DateTime.Now - c.EndedAt <= RecencyWindow).ToList();
        if (recent.Count == 0)
            return;

        var system = await settingsRepository.GetSystemSettingsAsync(ct);
        var baseUrl = string.IsNullOrWhiteSpace(system.PublicBaseUrl)
            ? "http://localhost:5210"
            : system.PublicBaseUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(system.PublicBaseUrl))
            logger.LogWarning("PublicBaseUrl not configured — alert links default to {Url}.", baseUrl);

        foreach (var channel in channels)
        {
            var rules = await settingsRepository.GetAlertSettingsAsync(channel.Channel, ct);
            if (!rules.Enabled || rules.Recipients.Count == 0 || rules.TriggerClasses.Count == 0)
                continue;

            var triggers = new HashSet<string>(rules.TriggerClasses, StringComparer.OrdinalIgnoreCase);
            foreach (var capture in recent)
            {
                if (!triggers.Contains(capture.ObjectClass))
                    continue;

                var alert = new CaptureAlert(
                    capture,
                    DetectableClasses.Translate(capture.ObjectClass),
                    $"{baseUrl}/captures/{capture.Id}/play",
                    ResolveThumbnail(capture));
                try
                {
                    if (await channel.TrySendAsync(alert, rules, system, ct))
                        logger.LogInformation(
                            "Alert sent via {Channel} for capture {CaptureId} ({Class} @ {Camera}).",
                            channel.Channel, capture.Id, capture.ObjectClass, capture.CameraName);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Alert via {Channel} failed for capture {CaptureId}.",
                        channel.Channel, capture.Id);
                }
            }
        }
    }

    private string? ResolveThumbnail(Capture capture)
    {
        if (capture.ThumbnailPath == null)
            return null;
        var path = Path.Combine(storage.OutputRoot,
            capture.ThumbnailPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? path : null;
    }
}
