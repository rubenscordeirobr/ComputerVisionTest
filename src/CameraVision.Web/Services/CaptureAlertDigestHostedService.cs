using System.Net;
using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;

namespace CameraVision.Web.Services;

/// <summary>
/// Antiflood for capture alerts: sends at most one grouped summary per configured
/// window (per channel) covering every capture the dispatcher queued
/// (AlertQueuedAt set, AlertSentAt null). Runs only in the web app; the API just
/// queues. A burst of detections becomes a single PT-BR e-mail with one tokenized
/// playback link per capture.
/// </summary>
public sealed class CaptureAlertDigestHostedService(
    ISettingsRepository settingsRepository,
    ICaptureRepository captures,
    IEnumerable<IAlertChannel> channels,
    CaptureLinkService captureLinks,
    StoragePaths storage,
    ILogger<CaptureAlertDigestHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunDigestCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Capture alert digest cycle failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task RunDigestCycleAsync(CancellationToken ct)
    {
        var pending = await captures.GetPendingAlertsAsync(100, ct);
        if (pending.Count == 0)
            return;

        var settings = await settingsRepository.GetCaptureAlertSettingsAsync(ct);
        var now = DateTime.Now;
        var window = TimeSpan.FromMinutes(Math.Max(1, settings.GroupWindowMinutes));
        // With grouping later disabled, leftovers still flush as one final summary.
        if (settings.GroupingEnabled &&
            settings.LastDigestAt != null && now - settings.LastDigestAt < window)
            return;

        var system = await settingsRepository.GetSystemSettingsAsync(ct);
        var baseUrl = system.PublicBaseUrl.Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
            baseUrl = captureLinks.PublicBaseUrl;
        if (baseUrl.Length == 0)
            baseUrl = "http://localhost:5210";

        // One summary per tenant per channel — recipients are tenant-scoped (SPEC-14).
        foreach (var tenantCaptures in pending.GroupBy(c => c.TenantId))
        {
            foreach (var channel in channels)
            {
                var items = tenantCaptures
                    .Where(c => (c.AlertChannels ?? "").Contains(channel.Channel.ToString(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (items.Count == 0)
                    continue;

                var alertSettings = await settingsRepository.GetAlertSettingsAsync(
                    tenantCaptures.Key, channel.Channel, ct);
                if (!alertSettings.Enabled || alertSettings.Recipients.Count == 0)
                    continue;

                try
                {
                    if (await channel.TrySendAsync(ComposeDigest(items, baseUrl), alertSettings, system, ct))
                        logger.LogInformation("Grouped capture alert sent via {Channel} with {Count} capture(s).",
                            channel.Channel, items.Count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Grouped capture alert via {Channel} failed.", channel.Channel);
                }
            }
        }

        // Marked regardless of channel outcome so a broken SMTP never loops the same batch.
        await captures.MarkAlertsSentAsync(pending.Select(c => c.Id), now, ct);
        settings.LastDigestAt = now;
        await settingsRepository.SaveCaptureAlertSettingsAsync(settings, ct);
    }

    private AlertMessage ComposeDigest(IReadOnlyList<Capture> items, string baseUrl)
    {
        var textLines = items.Select(c =>
            $"- {c.StartedAt:HH:mm:ss} — {DetectableClasses.Translate(c.ObjectClass)} em {c.CameraName} " +
            $"({c.Duration:mm\\:ss}): {captureLinks.PlaybackUrl(c.Id, baseUrl)}");
        var text =
            $"Resumo de capturas — CameraVision\n\n{items.Count} nova(s) captura(s):\n\n" +
            string.Join("\n", textLines) +
            "\n\nCameraVision — resumo automático, não responda.";

        var htmlItems = string.Join("", items.Select(c =>
            "<li style=\"margin:0 0 6px\">" +
            $"<b>{c.StartedAt:HH:mm:ss}</b> — {WebUtility.HtmlEncode(DetectableClasses.Translate(c.ObjectClass))} " +
            $"em <b>{WebUtility.HtmlEncode(c.CameraName)}</b> ({c.Duration:mm\\:ss}) " +
            $"<a href=\"{captureLinks.PlaybackUrl(c.Id, baseUrl)}\" style=\"color:#594ae2\">Assistir</a>" +
            "</li>"));

        var firstThumbnail = items
            .Select(ResolveThumbnail)
            .FirstOrDefault(path => path != null);
        var thumbnailHtml = firstThumbnail == null
            ? ""
            : "<p style=\"margin:0 0 16px\"><img src=\"cid:inline-image@cameravision\" " +
              "alt=\"Miniatura da primeira captura\" style=\"max-width:100%;border-radius:6px\" /></p>";

        var html =
            "<div style=\"font-family:Roboto,Arial,sans-serif;max-width:560px\">" +
            "<h2 style=\"color:#594ae2;margin:0 0 12px\">Resumo de capturas</h2>" +
            $"<p style=\"margin:0 0 16px\">{items.Count} nova(s) captura(s) no período.</p>" +
            thumbnailHtml +
            $"<ul style=\"margin:0 0 16px;padding-left:20px\">{htmlItems}</ul>" +
            "<p style=\"color:#888;font-size:12px\">CameraVision — resumo automático, não responda.</p>" +
            "</div>";

        return new AlertMessage(
            $"Resumo de capturas — {items.Count} nova(s) captura(s)",
            html, text, firstThumbnail);
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
