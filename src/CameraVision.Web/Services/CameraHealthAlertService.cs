using System.Collections.Concurrent;
using System.Net;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Health;
using CameraVision.Core.Repositories;

namespace CameraVision.Web.Services;

/// <summary>
/// Health-alert state machine fed by every probe cycle (SPEC-13).
///
/// Classification per cycle: Offline (probe failed), Weak (online but latency above
/// the threshold, or intermittent failures), Healthy otherwise. A camera's alert
/// state only transitions after the condition holds for ConsecutiveChecks cycles.
///
/// Notification precedence for each recorded event: cooldown → flood cap → digest.
/// With digest mode on, nothing is sent individually — events wait for the digest
/// job. Suppressed events stay in history and ride the next digest.
/// </summary>
public sealed class CameraHealthAlertService(
    ISettingsRepository settingsRepository,
    ICameraHealthEventRepository events,
    HealthAlertNotifier notifier,
    ILogger<CameraHealthAlertService> logger) : ICameraHealthCycleListener
{
    private enum Classified
    {
        Healthy,
        Offline,
        Weak,
    }

    private sealed class CameraState
    {
        public HealthCondition? AlertState;
        public Classified? Candidate;
        public int CandidateStreak;
        public readonly Queue<bool> RecentFailures = new();
        public string? LastDetail;
    }

    private readonly ConcurrentDictionary<int, CameraState> _states = new();

    public async Task OnCycleAsync(IReadOnlyList<(Camera Camera, CameraHealth Health)> results,
        CancellationToken ct)
    {
        var settings = await settingsRepository.GetHealthAlertSettingsAsync(ct);
        if (!settings.Enabled)
            return;

        foreach (var (camera, health) in results)
        {
            if (!camera.Enabled || health.Status is CameraStatus.Disabled or CameraStatus.Unknown)
            {
                _states.TryRemove(camera.Id, out _);
                continue;
            }

            var state = _states.GetOrAdd(camera.Id, _ => new CameraState());
            var classified = Classify(health, settings, state);

            if (classified != state.Candidate)
            {
                state.Candidate = classified;
                state.CandidateStreak = 1;
            }
            else
            {
                state.CandidateStreak++;
            }

            if (state.CandidateStreak < Math.Max(1, settings.ConsecutiveChecks))
                continue;

            if (classified == Classified.Healthy)
            {
                if (state.AlertState != null)
                {
                    state.AlertState = null;
                    await RecordEventAsync(camera, HealthCondition.Recovered, null, settings,
                        wantsNotification: settings.NotifyRecovery, ct);
                }
            }
            else
            {
                var condition = classified == Classified.Offline ? HealthCondition.Offline : HealthCondition.Weak;
                if (state.AlertState != condition)
                {
                    state.AlertState = condition;
                    await RecordEventAsync(camera, condition, state.LastDetail, settings,
                        wantsNotification: true, ct);
                }
            }
        }
    }

    private Classified Classify(CameraHealth health, HealthAlertSettings settings, CameraState state)
    {
        var failed = health.Status == CameraStatus.Offline;
        state.RecentFailures.Enqueue(failed);
        while (state.RecentFailures.Count > Math.Max(2, settings.ConsecutiveChecks * 2))
            state.RecentFailures.Dequeue();

        if (failed)
        {
            state.LastDetail = null;
            return Classified.Offline;
        }

        var latency = health.PingMs ?? health.ConnectMs;
        if (latency is { } ms && ms > settings.WeakLatencyMs)
        {
            state.LastDetail = $"latência {ms} ms";
            return Classified.Weak;
        }

        // Intermittent: enough failures inside the recent window without ever
        // reaching the consecutive threshold that would flag Offline.
        var failures = state.RecentFailures.Count(f => f);
        if (failures >= settings.ConsecutiveChecks)
        {
            state.LastDetail = "falhas intermitentes";
            return Classified.Weak;
        }

        state.LastDetail = null;
        return Classified.Healthy;
    }

    private async Task RecordEventAsync(Camera camera, HealthCondition condition, string? detail,
        HealthAlertSettings settings, bool wantsNotification, CancellationToken ct)
    {
        var now = DateTime.Now;
        var healthEvent = new CameraHealthEvent
        {
            CameraId = camera.Id,
            CameraName = camera.Name,
            Condition = condition,
            Detail = detail,
            OccurredAt = now,
        };

        if (!wantsNotification)
        {
            // Recorded for history only (e.g. recovery with notifications off).
            healthEvent.DigestedAt = now;
        }
        else if (settings.DigestEnabled)
        {
            // Digest mode: no individual messages — the digest job picks it up.
        }
        else
        {
            // 1. Cooldown (same camera + condition)
            var lastNotified = await events.GetLastNotifiedAtAsync(camera.Name, condition, ct);
            if (lastNotified != null && now - lastNotified < TimeSpan.FromMinutes(settings.CooldownMinutes))
            {
                healthEvent.Suppressed = true;
            }
            else
            {
                // 2. Global flood cap
                var windowStart = now - TimeSpan.FromMinutes(Math.Max(1, settings.FloodCapWindowMinutes));
                var sentInWindow = await events.CountNotifiedSinceAsync(windowStart, ct);
                if (sentInWindow >= Math.Max(1, settings.FloodCapCount))
                {
                    healthEvent.Suppressed = true;
                    logger.LogWarning(
                        "Health alert for {Camera} held: flood cap reached ({Count}/{Cap} in {Window} min).",
                        camera.Name, sentInWindow, settings.FloodCapCount, settings.FloodCapWindowMinutes);
                }
                else
                {
                    // 3. Send individually now.
                    await notifier.SendAsync(ComposeMessage(camera.Name, condition, detail, now), settings, ct);
                    healthEvent.NotifiedAt = now;
                    logger.LogInformation("Health alert sent: {Camera} {Condition}.", camera.Name, condition);
                }
            }
        }

        await events.AddAsync(healthEvent, ct);
    }

    public static string ConditionLabelPtBr(HealthCondition condition) => condition switch
    {
        HealthCondition.Offline => "offline",
        HealthCondition.Weak => "sinal fraco",
        HealthCondition.Recovered => "normalizada",
        _ => condition.ToString(),
    };

    private static AlertMessage ComposeMessage(string cameraName, HealthCondition condition,
        string? detail, DateTime at)
    {
        var (subject, line) = condition switch
        {
            HealthCondition.Offline => (
                $"Câmera {cameraName} está offline",
                $"A câmera <b>{WebUtility.HtmlEncode(cameraName)}</b> ficou <b>offline</b> (inacessível)."),
            HealthCondition.Weak => (
                $"Câmera {cameraName} com sinal fraco",
                $"A câmera <b>{WebUtility.HtmlEncode(cameraName)}</b> está com <b>sinal fraco</b>" +
                (detail == null ? "." : $" ({WebUtility.HtmlEncode(detail)}).")),
            _ => (
                $"Câmera {cameraName} normalizada",
                $"A câmera <b>{WebUtility.HtmlEncode(cameraName)}</b> voltou ao normal."),
        };

        var text = $"{subject}\n\nDetectado às {at:dd/MM/yyyy HH:mm:ss}." +
                   (detail == null ? "" : $"\nDetalhe: {detail}") +
                   "\n\nCameraVision — alerta automático.";
        var html =
            "<div style=\"font-family:Roboto,Arial,sans-serif;max-width:520px\">" +
            "<h2 style=\"color:#594ae2;margin:0 0 12px\">Saúde das câmeras</h2>" +
            $"<p style=\"margin:0 0 8px\">{line}</p>" +
            $"<p style=\"margin:0 0 16px;color:#666\">Detectado às {at:dd/MM/yyyy HH:mm:ss}.</p>" +
            "<p style=\"color:#888;font-size:12px\">CameraVision — alerta automático, não responda.</p>" +
            "</div>";
        return new AlertMessage(subject, html, text);
    }
}
