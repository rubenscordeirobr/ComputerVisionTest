using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;

namespace CameraVision.Web.Services;

/// <summary>
/// Sends critical system alerts to the system administrators through the
/// channels enabled in AdminAlertSettings. The admin recipients ride the same
/// channel implementations used by tenant alerts, via transient AlertSettings
/// that are never persisted.
/// </summary>
public sealed class AdminAlertNotifier(
    IEnumerable<IAlertChannel> channels,
    ISettingsRepository settingsRepository,
    ILogger<AdminAlertNotifier> logger)
{
    /// <summary>Returns true when at least one channel delivered the message.</summary>
    public async Task<bool> SendAsync(AlertMessage message, AdminAlertSettings admin,
        CancellationToken ct = default)
    {
        var system = await settingsRepository.GetSystemSettingsAsync(ct);
        var anySent = false;

        foreach (var channel in channels)
        {
            var recipients = channel.Channel switch
            {
                AlertChannel.Email when admin.NotifyEmail => admin.Emails,
                AlertChannel.WhatsApp when admin.NotifyWhatsApp => admin.WhatsAppNumbers,
                _ => null,
            };
            if (recipients == null || recipients.Count == 0)
                continue;

            var settings = new AlertSettings
            {
                Channel = channel.Channel,
                Enabled = true,
                Recipients = recipients,
            };

            try
            {
                if (await channel.TrySendAsync(message, settings, system, ct))
                    anySent = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Admin alert via {Channel} failed.", channel.Channel);
            }
        }

        return anySent;
    }
}
