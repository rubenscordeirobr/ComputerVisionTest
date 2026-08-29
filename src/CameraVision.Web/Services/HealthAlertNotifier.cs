using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;

namespace CameraVision.Web.Services;

/// <summary>
/// Sends a health AlertMessage through the channels enabled in HealthAlertSettings,
/// respecting each channel's own master switch and recipients. Shared by the
/// individual health alerts and the digest job.
/// </summary>
public sealed class HealthAlertNotifier(
    IEnumerable<IAlertChannel> channels,
    ISettingsRepository settingsRepository,
    ILogger<HealthAlertNotifier> logger)
{
    public async Task<bool> SendAsync(AlertMessage message, HealthAlertSettings health,
        CancellationToken ct = default)
    {
        var system = await settingsRepository.GetSystemSettingsAsync(ct);
        var anySent = false;

        foreach (var channel in channels)
        {
            var wanted = channel.Channel switch
            {
                AlertChannel.Email => health.NotifyEmail,
                AlertChannel.WhatsApp => health.NotifyWhatsApp,
                _ => false,
            };
            if (!wanted)
                continue;

            var settings = await settingsRepository.GetAlertSettingsAsync(channel.Channel, ct);
            if (!settings.Enabled || settings.Recipients.Count == 0)
                continue;

            try
            {
                if (await channel.TrySendAsync(message, settings, system, ct))
                    anySent = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Health alert via {Channel} failed.", channel.Channel);
            }
        }

        return anySent;
    }
}
