using System.Net;
using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;

namespace CameraVision.Web.Services;

/// <summary>
/// Antiflood for capture alerts: sends at most one grouped summary per tenant per
/// configured window (per channel) covering every capture the dispatcher queued
/// (AlertQueuedAt set, AlertSentAt null). Runs only in the web app; the API just
/// queues. A burst of detections becomes a single PT-BR e-mail with one tokenized
/// playback link per capture. Every delivery attempt is recorded as a
/// CaptureAlertLog row per capture.
/// </summary>
public sealed class CaptureAlertDigestHostedService(
    ISettingsRepository settingsRepository,
    ICaptureRepository captures,
    ICaptureAlertLogRepository alertLogRepository,
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

        var system = await settingsRepository.GetSystemSettingsAsync(ct);
        var baseUrl = system.PublicBaseUrl.Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
            baseUrl = captureLinks.PublicBaseUrl;
        if (baseUrl.Length == 0)
            baseUrl = "http://localhost:5210";

        // One summary per tenant per channel — recipients and the antiflood
        // window are tenant-scoped (SPEC-14), so each tenant flushes on its own.
        foreach (var tenantCaptures in pending.GroupBy(c => c.TenantId))
            await DigestTenantAsync(tenantCaptures.Key, [.. tenantCaptures], system, baseUrl, ct);
    }

    private async Task DigestTenantAsync(int tenantId, IReadOnlyList<Capture> items,
        SystemSettings system, string baseUrl, CancellationToken ct)
    {
        var settings = await settingsRepository.GetCaptureAlertSettingsAsync(tenantId, ct);
        var now = DateTime.Now;
        var window = TimeSpan.FromMinutes(Math.Max(1, settings.GroupWindowMinutes));
        // With grouping later disabled, leftovers still flush as one final summary.
        if (settings.GroupingEnabled &&
            settings.LastDigestAt != null && now - settings.LastDigestAt < window)
            return;

        var logs = new List<CaptureAlertLog>();
        foreach (var channel in channels)
        {
            var channelItems = items
                .Where(c => (c.AlertChannels ?? "").Contains(channel.Channel.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (channelItems.Count == 0)
                continue;

            var alertSettings = await settingsRepository.GetAlertSettingsAsync(tenantId, channel.Channel, ct);

            string? error;
            if (!alertSettings.Enabled || alertSettings.Recipients.Count == 0)
            {
                error = "Canal desativado ou sem destinatários configurados.";
            }
            else
            {
                try
                {
                    if (await channel.TrySendAsync(ComposeDigest(channelItems, baseUrl), alertSettings, system, ct))
                    {
                        error = null;
                        logger.LogInformation("Grouped capture alert sent via {Channel} with {Count} capture(s).",
                            channel.Channel, channelItems.Count);
                    }
                    else
                    {
                        error = "O canal não confirmou o envio (verifique as configurações e os destinatários).";
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    error = ex.Message;
                    logger.LogError(ex, "Grouped capture alert via {Channel} failed.", channel.Channel);
                }
            }

            // AlertRuleId is null only for captures queued before the column
            // existed or whose rule was deleted — those cannot be attributed.
            logs.AddRange(channelItems
                .Where(c => c.AlertRuleId != null)
                .Select(c => new CaptureAlertLog
                {
                    CaptureId = c.Id,
                    CaptureRuleId = c.AlertRuleId!.Value,
                    SentAt = now,
                    Channel = channel.Channel,
                    Status = error == null ? CaptureAlertStatus.Success : CaptureAlertStatus.Fail,
                    ErrorMessage = error,
                }));
        }

        // Marked regardless of channel outcome so a broken SMTP never loops the same batch.
        await captures.MarkAlertsSentAsync(items.Select(c => c.Id), now, ct);
        settings.LastDigestAt = now;
        await settingsRepository.SaveCaptureAlertSettingsAsync(settings, ct);

        try
        {
            await alertLogRepository.AddRangeAsync(logs, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to persist {Count} capture alert log row(s).", logs.Count);
        }
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
