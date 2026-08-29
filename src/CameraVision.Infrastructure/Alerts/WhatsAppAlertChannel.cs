using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Alerts;

/// <summary>
/// Placeholder for the future Evolution API sender. Registered so the channel is
/// visible in the dispatch pipeline; it only logs and reports "not sent".
/// </summary>
public sealed class WhatsAppAlertChannel(ILogger<WhatsAppAlertChannel> logger) : IAlertChannel
{
    public AlertChannel Channel => AlertChannel.WhatsApp;

    public Task<bool> TrySendAsync(AlertMessage message, AlertSettings settings,
        SystemSettings system, CancellationToken ct = default)
    {
        logger.LogInformation(
            "WhatsApp alert \"{Subject}\" skipped — sending is not implemented in v1.",
            message.Subject);
        return Task.FromResult(false);
    }
}
