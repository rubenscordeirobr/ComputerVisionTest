using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ISettingsRepository
{
    /// <summary>One tenant's channel master switch; a transient default (disabled) when unset.</summary>
    Task<AlertSettings> GetAlertSettingsAsync(int tenantId, AlertChannel channel, CancellationToken ct = default);

    Task SaveAlertSettingsAsync(AlertSettings settings, CancellationToken ct = default);

    Task<SystemSettings> GetSystemSettingsAsync(CancellationToken ct = default);
    Task SaveSystemSettingsAsync(SystemSettings settings, CancellationToken ct = default);

    Task<HealthAlertSettings> GetHealthAlertSettingsAsync(CancellationToken ct = default);
    Task SaveHealthAlertSettingsAsync(HealthAlertSettings settings, CancellationToken ct = default);

    Task<AdminAlertSettings> GetAdminAlertSettingsAsync(CancellationToken ct = default);
    Task SaveAdminAlertSettingsAsync(AdminAlertSettings settings, CancellationToken ct = default);
}
