using CameraVision.Core.Entities;

namespace CameraVision.Core.Alerts;

/// <summary>
/// Channel-agnostic message. HtmlBody may reference the inline image (when
/// InlineImagePath is set) via <c>cid:inline-image@cameravision</c>.
/// </summary>
public sealed record AlertMessage(
    string Subject,
    string HtmlBody,
    string TextBody,
    string? InlineImagePath = null);

/// <summary>
/// One alert delivery mechanism. Email is implemented; WhatsApp is a stub —
/// a future implementation replaces the stub without touching the callers.
/// </summary>
public interface IAlertChannel
{
    AlertChannel Channel { get; }

    /// <summary>Returns true when the message was actually delivered.</summary>
    Task<bool> TrySendAsync(AlertMessage message, AlertSettings settings, SystemSettings system,
        CancellationToken ct = default);
}

public interface IAlertDispatcher
{
    /// <summary>Evaluates capture rules for freshly imported captures. Never throws.</summary>
    Task DispatchAsync(IReadOnlyList<Capture> newCaptures, CancellationToken ct = default);
}
