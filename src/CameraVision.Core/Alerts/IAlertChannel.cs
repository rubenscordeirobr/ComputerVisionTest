using CameraVision.Core.Entities;

namespace CameraVision.Core.Alerts;

/// <summary>Everything a channel needs to notify about one capture.</summary>
public sealed record CaptureAlert(
    Capture Capture,
    string ClassLabelPtBr,
    string PlaybackUrl,
    string? ThumbnailAbsolutePath);

/// <summary>
/// One alert delivery mechanism. Email is implemented in v1; WhatsApp is a stub —
/// a future implementation replaces the stub without touching the dispatcher.
/// </summary>
public interface IAlertChannel
{
    AlertChannel Channel { get; }

    /// <summary>Returns true when the alert was actually delivered.</summary>
    Task<bool> TrySendAsync(CaptureAlert alert, AlertSettings rules, SystemSettings system,
        CancellationToken ct = default);
}

public interface IAlertDispatcher
{
    /// <summary>Evaluates alert rules for freshly imported captures. Never throws.</summary>
    Task DispatchAsync(IReadOnlyList<Capture> newCaptures, CancellationToken ct = default);
}
