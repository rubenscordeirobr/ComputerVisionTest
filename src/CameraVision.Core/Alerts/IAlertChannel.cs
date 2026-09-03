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
/// One delivery mechanism (e-mail via SMTP, WhatsApp via the Evolution API). The
/// recipients are passed explicitly; whether a channel is enabled for a tenant is
/// the caller's decision.
/// </summary>
public interface IAlertChannel
{
    AlertChannel Channel { get; }

    /// <summary>Returns true when the message was delivered to at least one recipient.</summary>
    Task<bool> TrySendAsync(AlertMessage message, IReadOnlyList<string> recipients, SystemSettings system,
        CancellationToken ct = default);
}

public interface IAlertDispatcher
{
    /// <summary>Evaluates capture rules for freshly imported captures and queues deliveries. Never throws.</summary>
    Task DispatchAsync(IReadOnlyList<Capture> newCaptures, CancellationToken ct = default);
}
