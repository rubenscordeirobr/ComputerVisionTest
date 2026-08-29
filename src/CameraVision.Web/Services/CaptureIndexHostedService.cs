using CameraVision.Core;
using CameraVision.Core.Alerts;

namespace CameraVision.Web.Services;

/// <summary>
/// Runs the capture importer at startup and then on a fixed interval, and hands
/// freshly imported captures to the alert dispatcher.
/// </summary>
public sealed class CaptureIndexHostedService(
    ICaptureIndexer indexer,
    IAlertDispatcher alertDispatcher,
    IConfiguration configuration,
    ILogger<CaptureIndexHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(10, configuration.GetValue("CaptureIndex:IntervalSeconds", 60)));
        using var timer = new PeriodicTimer(interval);

        try
        {
            do
            {
                try
                {
                    var result = await indexer.ScanAsync(stoppingToken);
                    if (result.AddedCaptures.Count > 0)
                        await alertDispatcher.DispatchAsync(result.AddedCaptures, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Capture index cycle failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}
