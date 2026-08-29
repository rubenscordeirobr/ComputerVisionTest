using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CameraVision.Core.Entities;
using CameraVision.Core.Health;
using CameraVision.Core.Repositories;

namespace CameraVision.Web.Services;

/// <summary>
/// Periodically probes every enabled camera: ICMP ping for latency (tolerates blocked
/// ICMP) and a TCP connect to the stream port for the online/offline verdict.
/// </summary>
public sealed class CameraHealthMonitor(
    ICameraRepository cameras,
    IConfiguration configuration,
    ILogger<CameraHealthMonitor> logger) : BackgroundService, ICameraHealthService
{
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    private readonly ConcurrentDictionary<int, CameraHealth> _health = new();

    public event Action? Changed;

    public CameraHealth? TryGet(int cameraId) =>
        _health.TryGetValue(cameraId, out var health) ? health : null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(5, configuration.GetValue("HealthCheck:IntervalSeconds", 15)));
        using var timer = new PeriodicTimer(interval);

        try
        {
            do
            {
                try
                {
                    await CheckAllAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Camera health cycle failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task CheckAllAsync(CancellationToken ct)
    {
        var list = await cameras.GetAllAsync(ct);
        var results = await Task.WhenAll(list.Select(c => CheckAsync(c, ct)));

        var knownIds = list.Select(c => c.Id).ToHashSet();
        foreach (var staleId in _health.Keys.Where(id => !knownIds.Contains(id)).ToList())
            _health.TryRemove(staleId, out _);

        foreach (var health in results)
            _health[health.CameraId] = health;

        Changed?.Invoke();
    }

    private static async Task<CameraHealth> CheckAsync(Camera camera, CancellationToken ct)
    {
        if (!camera.Enabled)
            return new CameraHealth(camera.Id, CameraStatus.Disabled, null, null, DateTime.Now);

        var (host, port) = ResolveTarget(camera);
        if (host is null)
            return new CameraHealth(camera.Id, CameraStatus.Unknown, null, null, DateTime.Now);

        long? pingMs = null;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, PingTimeout, cancellationToken: ct);
            if (reply.Status == IPStatus.Success)
                pingMs = reply.RoundtripTime;
        }
        catch when (!ct.IsCancellationRequested)
        {
            // ICMP unavailable/blocked — latency falls back to the TCP connect time.
        }

        long? connectMs = null;
        var online = false;
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ConnectTimeout);
            var stopwatch = Stopwatch.StartNew();
            await client.ConnectAsync(host, port, timeoutCts.Token);
            connectMs = stopwatch.ElapsedMilliseconds;
            online = true;
        }
        catch when (!ct.IsCancellationRequested)
        {
            // refused/timeout → offline
        }

        return new CameraHealth(camera.Id, online ? CameraStatus.Online : CameraStatus.Offline,
            pingMs, connectMs, DateTime.Now);
    }

    /// <summary>Explicit IP wins; otherwise the stream URL's host. Null when neither exists.</summary>
    private static (string? Host, int Port) ResolveTarget(Camera camera)
    {
        var hasUri = Uri.TryCreate(camera.StreamUrl, UriKind.Absolute, out var uri) &&
                     !string.IsNullOrEmpty(uri!.Host);
        var port = hasUri && uri!.Port > 0 ? uri.Port : 554;

        if (!string.IsNullOrWhiteSpace(camera.IpAddress))
            return (camera.IpAddress.Trim(), port);
        if (hasUri)
            return (uri!.Host, port);
        return (null, 0);
    }
}
