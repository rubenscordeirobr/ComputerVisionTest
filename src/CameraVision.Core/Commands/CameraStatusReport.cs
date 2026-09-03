using CameraVision.Core.Health;

namespace CameraVision.Core.Commands;

/// <summary>One camera as the report sees it (probe result + last processor update).</summary>
public sealed record CameraStatusLine(
    string Name,
    bool Enabled,
    bool HasStream,
    CameraStatus? Probe,
    long? LatencyMs,
    DateTime? ProcessorStatusAt);

/// <summary>
/// PT-BR text of the "status" answer: the detection worker's liveness and one line
/// per camera, with the same labels the Câmeras page shows (Desativada / Sem stream /
/// Offline / Verificando… / Sem processamento / Online).
/// </summary>
public static class CameraStatusReport
{
    public static string Compose(WorkerHealthSnapshot? worker, IReadOnlyList<CameraStatusLine> cameras, DateTime now)
    {
        var lines = new List<string>
        {
            $"Status — CameraVision ({now:dd/MM HH:mm})",
            WorkerLine(worker, now),
            "",
        };
        if (cameras.Count == 0)
        {
            lines.Add("Nenhuma câmera cadastrada.");
        }
        else
        {
            lines.Add("Câmeras:");
            lines.AddRange(cameras.Select(c => $"• {c.Name} — {CameraLabel(c, now)}"));
        }
        return string.Join('\n', lines);
    }

    public static string WorkerLine(WorkerHealthSnapshot? worker, DateTime now)
    {
        const string prefix = "Processador de vídeo: ";
        if (worker == null)
            return prefix + "verificando…";
        if (!worker.EverSeen)
            return prefix + "nunca conectado — inicie o CameraVision.DetectionWorker.";
        var seen = worker.LastSeenAt!.Value;
        if (worker.IsStale)
            return prefix + $"parado — sem atualização desde {seen:dd/MM HH:mm:ss} ({TimeText.Ago(seen, now)}). " +
                   "Nenhuma detecção está sendo processada.";

        var device = string.IsNullOrWhiteSpace(worker.Device) ? "" : $" · {worker.Device}";
        var cameras = worker.ActiveCameras is { } count ? $" · {count} câmera(s)" : "";
        return prefix + $"em execução{device}{cameras} · último sinal {seen:HH:mm:ss}.";
    }

    public static string CameraLabel(CameraStatusLine camera, DateTime now)
    {
        if (!camera.Enabled)
            return "Desativada";
        if (!camera.HasStream)
            return "Sem stream";
        if (camera.Probe == CameraStatus.Offline)
            return "Offline";
        if (camera.Probe != CameraStatus.Online)
            return "Verificando…";

        // The camera answers, but "Online" also requires the worker to keep its status fresh.
        if (WorkerHealth.IsStale(camera.ProcessorStatusAt, now))
        {
            var since = camera.ProcessorStatusAt is { } at
                ? $"sem atualização desde {at:dd/MM HH:mm}"
                : "nunca processada";
            return $"Sem processamento ({since})";
        }

        return camera.LatencyMs is { } ms ? $"Online · {ms} ms" : "Online";
    }
}
