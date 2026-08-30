using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Alerts;

/// <summary>
/// Sends alerts through the Evolution API instance configured in system settings
/// (paired by QR code on the Sistema page). When the message carries a thumbnail
/// the alert goes out as an image with the text as caption; otherwise plain text.
/// Delivery counts as success when at least one recipient received the message.
/// </summary>
public sealed class WhatsAppAlertChannel(
    IEvolutionApiClient evolution,
    ILogger<WhatsAppAlertChannel> logger) : IAlertChannel
{
    public AlertChannel Channel => AlertChannel.WhatsApp;

    public async Task<bool> TrySendAsync(AlertMessage message, AlertSettings settings,
        SystemSettings system, CancellationToken ct = default)
    {
        var text = message.TextBody.Trim();

        byte[]? image = null;
        string fileName = "";
        if (message.InlineImagePath != null && File.Exists(message.InlineImagePath))
        {
            image = await File.ReadAllBytesAsync(message.InlineImagePath, ct);
            fileName = Path.GetFileName(message.InlineImagePath);
        }

        var sent = 0;
        foreach (var recipient in settings.Recipients)
        {
            var result = image != null
                ? await evolution.SendImageAsync(system, recipient, text, image, fileName, ct)
                : await evolution.SendTextAsync(system, recipient, text, ct);

            if (result.Success)
                sent++;
            else
                logger.LogWarning("WhatsApp alert \"{Subject}\" to {Recipient} failed: {Error}",
                    message.Subject, recipient, result.Error);
        }

        if (sent > 0)
            logger.LogInformation("WhatsApp alert \"{Subject}\" sent to {Sent}/{Total} recipient(s).",
                message.Subject, sent, settings.Recipients.Count);
        return sent > 0;
    }
}
