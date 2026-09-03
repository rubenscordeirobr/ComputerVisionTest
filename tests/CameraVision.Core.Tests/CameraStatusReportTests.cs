using CameraVision.Core.Commands;
using CameraVision.Core.Health;

namespace CameraVision.Core.Tests;

public class CameraStatusReportTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 14, 0, 0);

    private static WorkerHealthSnapshot Snapshot(DateTime? lastSeen, string? device = "CUDA (RTX 5060)", int? cameras = 3) =>
        new(lastSeen, lastSeen, device, cameras, HasProcessableCameras: true, CheckedAt: Now);

    [Fact]
    public void Running_worker_line_has_device_cameras_and_last_signal()
    {
        var line = CameraStatusReport.WorkerLine(Snapshot(Now.AddSeconds(-10)), Now);
        Assert.Equal("Processador de vídeo: em execução · CUDA (RTX 5060) · 3 câmera(s) · último sinal 13:59:50.", line);
    }

    [Fact]
    public void Stale_worker_line_says_since_when()
    {
        var line = CameraStatusReport.WorkerLine(Snapshot(Now.AddHours(-2)), Now);
        Assert.StartsWith("Processador de vídeo: parado — sem atualização desde 07/09 12:00:00 (há 2 h).", line);
    }

    [Fact]
    public void Never_seen_and_unknown_worker()
    {
        Assert.Contains("nunca conectado", CameraStatusReport.WorkerLine(Snapshot(null), Now));
        Assert.Contains("verificando", CameraStatusReport.WorkerLine(null, Now));
    }

    private static CameraStatusLine Camera(bool enabled = true, bool hasStream = true, CameraStatus? probe = CameraStatus.Online,
        long? latency = 12, DateTime? processorAt = null) =>
        new("Garagem", enabled, hasStream, probe, latency, processorAt ?? Now.AddSeconds(-5));

    [Fact]
    public void Camera_labels_follow_the_cameras_page()
    {
        Assert.Equal("Desativada", CameraStatusReport.CameraLabel(Camera(enabled: false), Now));
        Assert.Equal("Sem stream", CameraStatusReport.CameraLabel(Camera(hasStream: false), Now));
        Assert.Equal("Offline", CameraStatusReport.CameraLabel(Camera(probe: CameraStatus.Offline), Now));
        Assert.Equal("Verificando…", CameraStatusReport.CameraLabel(Camera(probe: null), Now));
        Assert.Equal("Online · 12 ms", CameraStatusReport.CameraLabel(Camera(), Now));
        Assert.Equal("Online", CameraStatusReport.CameraLabel(Camera(latency: null), Now));
    }

    [Fact]
    public void Online_camera_with_stale_processor_is_sem_processamento()
    {
        var stale = CameraStatusReport.CameraLabel(Camera(processorAt: Now.AddMinutes(-3)), Now);
        Assert.Equal("Sem processamento (sem atualização desde 07/09 13:57)", stale);

        var never = new CameraStatusLine("Garagem", true, true, CameraStatus.Online, 5, null);
        Assert.Equal("Sem processamento (nunca processada)", CameraStatusReport.CameraLabel(never, Now));
    }

    [Fact]
    public void Compose_lists_every_camera_with_a_bullet()
    {
        var text = CameraStatusReport.Compose(Snapshot(Now.AddSeconds(-10)),
            [Camera(), new CameraStatusLine("Portão", true, true, CameraStatus.Offline, null, null)], Now);

        var lines = text.Split('\n');
        Assert.Equal("Status — CameraVision (07/09 14:00)", lines[0]);
        Assert.StartsWith("Processador de vídeo: em execução", lines[1]);
        Assert.Equal("Câmeras:", lines[3]);
        Assert.Equal("• Garagem — Online · 12 ms", lines[4]);
        Assert.Equal("• Portão — Offline", lines[5]);
    }

    [Fact]
    public void Compose_without_cameras() =>
        Assert.Contains("Nenhuma câmera cadastrada.", CameraStatusReport.Compose(null, [], Now));
}
