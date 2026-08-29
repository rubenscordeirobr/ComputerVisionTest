using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ISettingsRepository
{
    /// <summary>One tenant's channel settings; a transient default (empty recipients) when unset.</summary>
    Task<AlertSettings> GetAlertSettingsAsync(int tenantId, AlertChannel channel, CancellationToken ct = default);

    Task SaveAlertSettingsAsync(AlertSettings settings, CancellationToken ct = default);

    Task<SystemSettings> GetSystemSettingsAsync(CancellationToken ct = default);
    Task SaveSystemSettingsAsync(SystemSettings settings, CancellationToken ct = default);

    Task<HealthAlertSettings> GetHealthAlertSettingsAsync(CancellationToken ct = default);
    Task SaveHealthAlertSettingsAsync(HealthAlertSettings settings, CancellationToken ct = default);

    Task<CaptureAlertSettings> GetCaptureAlertSettingsAsync(CancellationToken ct = default);
    Task SaveCaptureAlertSettingsAsync(CaptureAlertSettings settings, CancellationToken ct = default);
}
