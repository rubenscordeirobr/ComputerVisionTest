using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ISettingsRepository
{
    Task<CaptureSettings> GetCaptureSettingsAsync(CancellationToken ct = default);
    Task SaveCaptureSettingsAsync(CaptureSettings settings, CancellationToken ct = default);

    Task<AlertSettings> GetAlertSettingsAsync(AlertChannel channel, CancellationToken ct = default);
    Task SaveAlertSettingsAsync(AlertSettings settings, CancellationToken ct = default);

    Task<SystemSettings> GetSystemSettingsAsync(CancellationToken ct = default);
    Task SaveSystemSettingsAsync(SystemSettings settings, CancellationToken ct = default);
}
