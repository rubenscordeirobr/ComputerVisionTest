using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

/// <summary>Log row joined with the display name of the rule that requested the alert.</summary>
public sealed record CaptureAlertLogEntry(CaptureAlertLog Log, string RuleName);

public interface ICaptureAlertLogRepository
{
    Task AddRangeAsync(IEnumerable<CaptureAlertLog> logs, CancellationToken ct = default);

    /// <summary>All delivery attempts for one capture, newest first.</summary>
    Task<IReadOnlyList<CaptureAlertLogEntry>> GetByCaptureAsync(int captureId, CancellationToken ct = default);
}
