using System.Net;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Health;
using CameraVision.Core.Repositories;

namespace CameraVision.Web.Services;

/// <summary>
/// Tracks DetectionWorker liveness from the global heartbeat (with the newest
/// per-camera status as fallback for older workers) and exposes a snapshot to
/// the UI. When the worker stops updating for longer than
/// AdminAlertSettings.WorkerDownAfterSeconds it raises a critical system alert
/// to the system administrators (e-mail + WhatsApp), plus a recovery notice.
/// Every transition is persisted in SystemAlertEvents; notifications respect
/// the master switch, per-channel switches and the cooldown.
/// </summary>
public sealed class WorkerHealthMonitor(
    IWorkerStatusRepository workerStatus,
    ICameraRepository cameras,
    ISettingsRepository settingsRepository,
    ISystemAlertEventRepository events,
    AdminAlertNotifier notifier,
    IConfiguration configuration,
    ILogger<WorkerHealthMonitor> logger) : BackgroundService, IWorkerHealthService
{
    private readonly DateTime _monitorStartedAt = DateTime.Now;
    private bool? _workerDown;
    private DateTime? _downSince;

    public WorkerHealthSnapshot? Current { get; private set; }

    public event Action? Changed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(5, configuration.GetValue("WorkerHealth:IntervalSeconds", 10)));
        using var timer = new PeriodicTimer(interval);

        try
        {
            do
            {
                try
                {
                    await CheckAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Worker health cycle failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        var now = DateTime.Now;
        var status = await workerStatus.GetAsync(ct);
        var allCameras = await cameras.GetAllAsync(ct: ct);

        var lastSeen = status?.LastHeartbeatAt;
        var lastCameraUpdate = allCameras.Max(c => c.ProcessorStatusAt);
        if (lastCameraUpdate != null && (lastSeen == null || lastCameraUpdate > lastSeen))
            lastSeen = lastCameraUpdate;

        var hasProcessable = allCameras.Any(c => c.Enabled && !string.IsNullOrWhiteSpace(c.StreamUrl));

        Current = new WorkerHealthSnapshot(lastSeen, status?.StartedAt, status?.Device,
            status?.ActiveCameras, hasProcessable, now);
        Changed?.Invoke();

        var settings = await settingsRepository.GetAdminAlertSettingsAsync(ct);
        var downAfter = TimeSpan.FromSeconds(Math.Max(45, settings.WorkerDownAfterSeconds));

        // Never seen: only counts as down when there is something to process and the
        // monitor itself has been up long enough to have received a heartbeat.
        var isDown = lastSeen != null
            ? now - lastSeen.Value > downAfter
            : hasProcessable && now - _monitorStartedAt > downAfter;

        if (_workerDown == null)
        {
            // First verdict after web startup: reconcile against the persisted history
            // so a transition that happened while the web app was off still notifies.
            var lastEvent = await events.GetLastAsync(ct);
            var lastKnownDown = lastEvent?.Type == SystemAlertType.WorkerDown;
            if (isDown == lastKnownDown)
            {
                _workerDown = isDown;
                if (isDown)
                    _downSince = lastEvent?.OccurredAt ?? lastSeen ?? now;
                return;
            }
        }
        else if (_workerDown == isDown)
        {
            return;
        }

        if (isDown)
        {
            _workerDown = true;
            _downSince = lastSeen ?? now;
            var detail = lastSeen == null
                ? "O processador nunca se conectou ao sistema."
                : $"Sem atualização desde {lastSeen:dd/MM/yyyy HH:mm:ss}.";
            await RaiseAsync(SystemAlertType.WorkerDown, detail, settings, ct);
        }
        else
        {
            var downSince = _downSince;
            _workerDown = false;
            _downSince = null;
            var detail = downSince == null
                ? null
                : $"Ficou indisponível por {TimeText.Duration(now - downSince.Value)}.";
            await RaiseAsync(SystemAlertType.WorkerRecovered, detail, settings, ct);
        }
    }

    private async Task RaiseAsync(SystemAlertType type, string? detail, AdminAlertSettings settings,
        CancellationToken ct)
    {
        var now = DateTime.Now;
        var alertEvent = new SystemAlertEvent { Type = type, Detail = detail, OccurredAt = now };

        var wantsNotification = settings.Enabled &&
                                (type != SystemAlertType.WorkerRecovered || settings.NotifyRecovery);
        if (wantsNotification)
        {
            var lastNotified = await events.GetLastNotifiedAtAsync(type, ct);
            if (lastNotified != null &&
                now - lastNotified < TimeSpan.FromMinutes(Math.Max(1, settings.CooldownMinutes)))
            {
                logger.LogInformation("System alert {Type} held by cooldown.", type);
            }
            else if (await notifier.SendAsync(ComposeMessage(type, detail, now), settings, ct))
            {
                alertEvent.NotifiedAt = now;
            }
        }

        await events.AddAsync(alertEvent, ct);
        logger.LogWarning("System alert: {Type} — {Detail} (notified: {Notified}).",
            type, detail ?? "-", alertEvent.NotifiedAt != null);
    }

    public static string TypeLabelPtBr(SystemAlertType type) => type switch
    {
        SystemAlertType.WorkerDown => "Processador parado",
        SystemAlertType.WorkerRecovered => "Processador normalizado",
        _ => type.ToString(),
    };

    private static AlertMessage ComposeMessage(SystemAlertType type, string? detail, DateTime at)
    {
        var (subject, line) = type == SystemAlertType.WorkerDown
            ? ("🚨 CRÍTICO: processador de vídeo parado",
                "O processador de vídeo (CameraVision.DetectionWorker) <b>parou de responder</b>. " +
                "As câmeras não estão sendo monitoradas e nenhuma detecção está sendo gravada.")
            : ("✅ Processador de vídeo normalizado",
                "O processador de vídeo <b>voltou a responder</b>. Monitoramento e gravações normalizados.");

        var text = $"{subject}\n\n{line.Replace("<b>", "").Replace("</b>", "")}" +
                   (detail == null ? "" : $"\n{detail}") +
                   $"\n\nDetectado às {at:dd/MM/yyyy HH:mm:ss}." +
                   "\n\nCameraVision — alerta automático do sistema.";
        var html =
            "<div style=\"font-family:Roboto,Arial,sans-serif;max-width:520px\">" +
            "<h2 style=\"color:#594ae2;margin:0 0 12px\">Alerta do sistema</h2>" +
            $"<p style=\"margin:0 0 8px\">{line}</p>" +
            (detail == null ? "" : $"<p style=\"margin:0 0 8px\">{WebUtility.HtmlEncode(detail)}</p>") +
            $"<p style=\"margin:0 0 16px;color:#666\">Detectado às {at:dd/MM/yyyy HH:mm:ss}.</p>" +
            "<p style=\"color:#888;font-size:12px\">CameraVision — alerta automático do sistema, não responda.</p>" +
            "</div>";
        return new AlertMessage(subject, html, text);
    }
}
