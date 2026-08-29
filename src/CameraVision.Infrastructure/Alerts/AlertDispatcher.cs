using System.Net;
using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Alerts;

/// <summary>
/// Evaluates the capture rules for freshly imported captures: every enabled rule
/// whose classes contain the capture's class contributes its channels; the union
/// of channels is notified once per capture. Captures older than the recency
/// window never alert, so importing a historical backlog stays silent. Each
/// capture is only ever seen here once (the import/ingest is insert-once).
/// </summary>
public sealed class AlertDispatcher(
    IEnumerable<IAlertChannel> channels,
    ICaptureRuleRepository ruleRepository,
    ICaptureRepository captureRepository,
    ISettingsRepository settingsRepository,
    CaptureLinkService captureLinks,
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
        // The settings page wins when filled in; otherwise the deployment's
        // CaptureLinks:PublicBaseUrl from appsettings.json is used.
        var baseUrl = system.PublicBaseUrl.Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
            baseUrl = captureLinks.PublicBaseUrl;
        if (baseUrl.Length == 0)
        {
            baseUrl = "http://localhost:5210";
            logger.LogWarning("Public base URL not configured — alert links default to {Url}.", baseUrl);
        }

        var grouping = await settingsRepository.GetCaptureAlertSettingsAsync(ct);

        // Rules and recipients are tenant-scoped: a capture only matches its own
        // tenant's rules and only notifies that tenant's recipients (SPEC-14).
        foreach (var tenantCaptures in recent.GroupBy(c => c.TenantId))
        {
            await DispatchTenantAsync(tenantCaptures.Key, [.. tenantCaptures],
                grouping, system, baseUrl, ct);
        }
    }

    private async Task DispatchTenantAsync(int tenantId, IReadOnlyList<Capture> tenantCaptures,
        CaptureAlertSettings grouping, SystemSettings system, string baseUrl, CancellationToken ct)
    {
        var rules = await ruleRepository.GetEnabledAsync(tenantId, ct);
        if (rules.Count == 0)
            return;

        var channelSettings = new Dictionary<AlertChannel, AlertSettings>();
        foreach (var channel in channels)
            channelSettings[channel.Channel] = await settingsRepository.GetAlertSettingsAsync(tenantId, channel.Channel, ct);

        foreach (var capture in tenantCaptures)
        {
            var matching = rules
                .Where(r => r.Classes.Contains(capture.ObjectClass, StringComparer.OrdinalIgnoreCase) &&
                            r.IsActiveAt(TimeOnly.FromDateTime(capture.StartedAt)))
                .ToList();
            if (matching.Count == 0)
                continue;

            var wanted = new HashSet<AlertChannel>();
            if (matching.Any(r => r.NotifyEmail))
                wanted.Add(AlertChannel.Email);
            if (matching.Any(r => r.NotifyWhatsApp))
                wanted.Add(AlertChannel.WhatsApp);
            if (wanted.Count == 0)
                continue;

            // Channels are resolved now so a rule's time window closing later
            // cannot drop a queued capture from the grouped summary.
            capture.AlertChannels = string.Join(",", wanted);

            if (grouping.GroupingEnabled)
            {
                // Antiflood: no individual message — the web app's digest job sends
                // one grouped summary per window (CaptureAlertDigestHostedService).
                capture.AlertQueuedAt = DateTime.Now;
                await captureRepository.UpdateAsync(capture, ct);
                logger.LogInformation(
                    "Capture alert queued for grouped summary: capture {CaptureId} ({Class} @ {Camera}).",
                    capture.Id, capture.ObjectClass, capture.CameraName);
                continue;
            }

            var message = ComposeCaptureMessage(capture, baseUrl);

            foreach (var channel in channels)
            {
                if (!wanted.Contains(channel.Channel))
                    continue;
                var settings = channelSettings[channel.Channel];
                if (!settings.Enabled || settings.Recipients.Count == 0)
                    continue;

                try
                {
                    if (await channel.TrySendAsync(message, settings, system, ct))
                        logger.LogInformation(
                            "Capture alert sent via {Channel} for capture {CaptureId} ({Class} @ {Camera}).",
                            channel.Channel, capture.Id, capture.ObjectClass, capture.CameraName);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Capture alert via {Channel} failed for capture {CaptureId}.",
                        channel.Channel, capture.Id);
                }
            }

            capture.AlertSentAt = DateTime.Now;
            await captureRepository.UpdateAsync(capture, ct);
        }
    }

    private AlertMessage ComposeCaptureMessage(Capture capture, string baseUrl)
    {
        var labelRaw = DetectableClasses.Translate(capture.ObjectClass);
        // Tokenized link: the recipient plays this one capture without signing in.
        var playbackUrl = captureLinks.PlaybackUrl(capture.Id, baseUrl);
        var thumbnail = ResolveThumbnail(capture);

        var camera = WebUtility.HtmlEncode(capture.CameraName);
        var label = WebUtility.HtmlEncode(labelRaw);
        var started = capture.StartedAt.ToString("dd/MM/yyyy HH:mm:ss");
        var duration = capture.Duration.ToString(@"mm\:ss");

        var text =
            $"Alerta de captura — CameraVision\n\n" +
            $"Câmera: {capture.CameraName}\n" +
            $"Objeto: {labelRaw}\n" +
            $"Início: {started}\n" +
            $"Duração: {duration}\n\n" +
            $"Assista ao vídeo: {playbackUrl}\n";

        var thumbnailHtml = thumbnail == null
            ? ""
            : "<p style=\"margin:0 0 16px\"><img src=\"cid:inline-image@cameravision\" " +
              "alt=\"Miniatura da captura\" style=\"max-width:100%;border-radius:6px\" /></p>";
        var html =
            "<div style=\"font-family:Roboto,Arial,sans-serif;max-width:520px\">" +
            "<h2 style=\"color:#594ae2;margin:0 0 12px\">Alerta de captura</h2>" +
            $"<p style=\"margin:0 0 16px\">Um objeto <b>{label}</b> foi detectado na câmera <b>{camera}</b>.</p>" +
            thumbnailHtml +
            "<table style=\"border-collapse:collapse;margin:0 0 16px\">" +
            $"<tr><td style=\"padding:2px 12px 2px 0;color:#666\">Câmera</td><td><b>{camera}</b></td></tr>" +
            $"<tr><td style=\"padding:2px 12px 2px 0;color:#666\">Objeto</td><td><b>{label}</b></td></tr>" +
            $"<tr><td style=\"padding:2px 12px 2px 0;color:#666\">Início</td><td>{started}</td></tr>" +
            $"<tr><td style=\"padding:2px 12px 2px 0;color:#666\">Duração</td><td>{duration}</td></tr>" +
            "</table>" +
            $"<p><a href=\"{playbackUrl}\" style=\"display:inline-block;background:#594ae2;color:#ffffff;" +
            "padding:10px 20px;border-radius:4px;text-decoration:none\">Assistir vídeo</a></p>" +
            "<p style=\"color:#888;font-size:12px\">CameraVision — alerta automático, não responda.</p>" +
            "</div>";

        return new AlertMessage(
            $"Alerta de captura — {labelRaw} em {capture.CameraName}",
            html, text, thumbnail);
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
