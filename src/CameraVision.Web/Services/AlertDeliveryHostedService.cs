using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Alerts;

namespace CameraVision.Web.Services;

/// <summary>
/// The only sender of capture notifications. Every 10 s it takes the pending
/// AlertDelivery rows the dispatcher queued (API ingest or the web indexer), applies
/// each rule's antiflood window and sends one message per recipient: individual
/// messages when the rule's window is 0, otherwise one summary of the rule's captures
/// per recipient per window (a summary of a single capture is the individual message).
/// Rows are marked Sent/Failed right after each attempt — a failed attempt still
/// starts the window, so a broken SMTP never loops. Runs only in the web app; the
/// API just queues.
/// </summary>
public sealed class AlertDeliveryHostedService(
    IAlertDeliveryRepository deliveries,
    ISettingsRepository settingsRepository,
    IEnumerable<IAlertChannel> channels,
    CaptureAlertComposer composer,
    ILogger<AlertDeliveryHostedService> logger) : BackgroundService
{
    private const int BatchSize = 1000;
    private const int MaxErrorLength = 500;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Notification delivery cycle failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var pending = await deliveries.GetPendingAsync(BatchSize, ct);
        if (pending.Count == 0)
            return;

        var system = await settingsRepository.GetSystemSettingsAsync(ct);
        var baseUrl = composer.ResolveBaseUrl(system, out var usedFallback);
        if (usedFallback)
            logger.LogWarning("Public base URL not configured — notification links default to {Url}.", baseUrl);

        var lastAttempt = await deliveries.GetLastAttemptByRuleAsync(pending.Select(p => p.Rule.Id), ct);
        var channelByKind = channels.ToDictionary(c => c.Channel);
        var masterSwitches = new Dictionary<(int TenantId, AlertChannel Channel), bool>();

        foreach (var ruleGroup in pending.GroupBy(p => p.Rule.Id))
        {
            var rule = ruleGroup.First().Rule;
            var window = TimeSpan.FromMinutes(Math.Max(0, rule.GroupWindowMinutes));
            if (window > TimeSpan.Zero &&
                lastAttempt.TryGetValue(rule.Id, out var last) && DateTime.Now - last < window)
                continue; // inside the rule's window — rows stay pending until it ends

            foreach (var target in ruleGroup.GroupBy(p => (p.Delivery.Channel, p.Delivery.Recipient)))
            {
                ct.ThrowIfCancellationRequested();
                var rows = target.OrderBy(p => p.Delivery.QueuedAt).ThenBy(p => p.Delivery.Id).ToList();
                var (channelKind, recipient) = target.Key;
                var outcomes = new List<AlertDeliveryOutcome>();

                string? refusal = null;
                IAlertChannel? channel = null;
                if (string.IsNullOrWhiteSpace(recipient))
                    refusal = "Destinatário ausente.";
                else if (!channelByKind.TryGetValue(channelKind, out channel))
                    refusal = "Canal indisponível.";
                else if (!await IsChannelEnabledAsync(rule.TenantId, channelKind, masterSwitches, ct))
                    refusal = "Canal desativado nas configurações de alertas.";

                if (refusal != null)
                {
                    outcomes.AddRange(rows.Select(r =>
                        new AlertDeliveryOutcome(r.Delivery.Id, AlertDeliveryStatus.Failed, refusal)));
                }
                else if (window == TimeSpan.Zero)
                {
                    // "Imediato": every capture is its own message.
                    foreach (var row in rows)
                    {
                        var error = await SendAsync(channel!, composer.ComposeCapture(row.Capture, baseUrl),
                            recipient!, system, ct);
                        outcomes.Add(Outcome(row, error));
                    }
                }
                else
                {
                    var captures = rows.Select(r => r.Capture)
                        .DistinctBy(c => c.Id)
                        .OrderBy(c => c.StartedAt)
                        .ToList();
                    var message = captures.Count == 1
                        ? composer.ComposeCapture(captures[0], baseUrl)
                        : composer.ComposeDigest(captures, baseUrl);
                    var error = await SendAsync(channel!, message, recipient!, system, ct);
                    outcomes.AddRange(rows.Select(r => Outcome(r, error)));
                }

                // Marked right away: a crash between send and mark must not re-send the batch.
                await deliveries.MarkAsync(outcomes, DateTime.Now, ct);
            }
        }
    }

    private static AlertDeliveryOutcome Outcome(PendingDelivery row, string? error) =>
        new(row.Delivery.Id, error == null ? AlertDeliveryStatus.Sent : AlertDeliveryStatus.Failed, error);

    private async Task<bool> IsChannelEnabledAsync(int tenantId, AlertChannel channel,
        Dictionary<(int TenantId, AlertChannel Channel), bool> cache, CancellationToken ct)
    {
        if (!cache.TryGetValue((tenantId, channel), out var enabled))
        {
            enabled = (await settingsRepository.GetAlertSettingsAsync(tenantId, channel, ct)).Enabled;
            cache[(tenantId, channel)] = enabled;
        }
        return enabled;
    }

    /// <summary>Null on success, otherwise the PT-BR reason stored on the delivery rows.</summary>
    private async Task<string?> SendAsync(IAlertChannel channel, AlertMessage message, string recipient,
        SystemSettings system, CancellationToken ct)
    {
        try
        {
            if (await channel.TrySendAsync(message, [recipient], system, ct))
            {
                logger.LogInformation("Notification \"{Subject}\" sent via {Channel} to {Recipient}.",
                    message.Subject, channel.Channel, recipient);
                return null;
            }
            return "O canal não confirmou o envio (verifique as configurações e o destinatário).";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Notification via {Channel} to {Recipient} failed.", channel.Channel, recipient);
            var reason = ex.Message;
            return reason.Length > MaxErrorLength ? reason[..MaxErrorLength] : reason;
        }
    }
}
