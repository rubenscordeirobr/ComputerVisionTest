using System.Net;
using CameraVision.Core.Alerts;
using CameraVision.Core.Repositories;

namespace CameraVision.Web.Services;

/// <summary>
/// When digest mode is on, groups all pending health events into one PT-BR summary
/// message per interval (e.g. "Resumo: 3 eventos — Garagem offline 14:02; ...").
/// Suppressed/held events are included, so nothing is ever lost.
/// </summary>
public sealed class HealthDigestHostedService(
    ISettingsRepository settingsRepository,
    ICameraHealthEventRepository events,
    HealthAlertNotifier notifier,
    ILogger<HealthDigestHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
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
                    logger.LogError(ex, "Health digest cycle failed.");
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
        var settings = await settingsRepository.GetHealthAlertSettingsAsync(ct);
        if (!settings.Enabled || !settings.DigestEnabled)
            return;

        var now = DateTime.Now;
        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.DigestIntervalMinutes));
        if (settings.LastDigestAt != null && now - settings.LastDigestAt < interval)
            return;

        var pending = await events.GetPendingForDigestAsync(100, ct);
        if (pending.Count == 0)
            return;

        var parts = pending.Select(e =>
            $"{e.CameraName} {CameraHealthAlertService.ConditionLabelPtBr(e.Condition)} {e.OccurredAt:HH:mm}");
        var summary = $"Resumo: {pending.Count} evento(s) — {string.Join("; ", parts)}.";

        var htmlItems = string.Join("", pending.Select(e =>
            $"<li><b>{WebUtility.HtmlEncode(e.CameraName)}</b> " +
            $"{CameraHealthAlertService.ConditionLabelPtBr(e.Condition)} às {e.OccurredAt:HH:mm}" +
            (e.Detail == null ? "" : $" ({WebUtility.HtmlEncode(e.Detail)})") + "</li>"));
        var html =
            "<div style=\"font-family:Roboto,Arial,sans-serif;max-width:520px\">" +
            "<h2 style=\"color:#594ae2;margin:0 0 12px\">Resumo de saúde das câmeras</h2>" +
            $"<ul style=\"margin:0 0 16px;padding-left:20px\">{htmlItems}</ul>" +
            "<p style=\"color:#888;font-size:12px\">CameraVision — resumo automático, não responda.</p>" +
            "</div>";

        var message = new AlertMessage(
            $"Resumo de saúde das câmeras — {pending.Count} evento(s)", html, summary);
        await notifier.SendAsync(message, settings, ct);

        await events.MarkDigestedAsync(pending.Select(e => e.Id), now, ct);
        settings.LastDigestAt = now;
        await settingsRepository.SaveHealthAlertSettingsAsync(settings, ct);
        logger.LogInformation("Health digest sent with {Count} event(s).", pending.Count);
    }
}
