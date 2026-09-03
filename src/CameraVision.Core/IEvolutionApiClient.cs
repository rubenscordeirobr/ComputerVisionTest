using CameraVision.Core.Entities;

namespace CameraVision.Core;

public enum EvolutionConnection
{
    Open,
    Connecting,
    Closed,
    Error,
}

/// <summary>QR/pairing data returned by the Evolution API. Error is PT-BR, shown as-is in the UI.</summary>
public sealed record EvolutionQr(string? Base64Image, string? PairingCode, string? Error = null);

public sealed record EvolutionState(EvolutionConnection Connection, string? Error = null);

/// <summary>Outcome of a message send. Error is PT-BR, suitable for logs and UI.</summary>
public sealed record EvolutionSendResult(bool Success, string? Error = null);

/// <summary>Webhook registered on the instance (GET webhook/find). Base64 = media arrives inline. Error is PT-BR.</summary>
public sealed record EvolutionWebhookState(bool Enabled, string? Url, string? Error = null, bool Base64 = false);

/// <summary>A media file downloaded through the instance (POST chat/getBase64FromMediaMessage).</summary>
public sealed record EvolutionMediaResult(bool Success, byte[]? Bytes = null, string? MimeType = null, string? Error = null);

/// <summary>
/// Minimal Evolution API client for the WhatsApp pairing flow (connect → QR → state)
/// and message sending. Never throws — failures come back as Error values.
/// </summary>
public interface IEvolutionApiClient
{
    Task<EvolutionQr> ConnectAsync(SystemSettings settings, CancellationToken ct = default);
    Task<EvolutionState> GetStateAsync(SystemSettings settings, CancellationToken ct = default);

    /// <summary>Sends a plain text message. <paramref name="number"/> may contain +, spaces or dashes.</summary>
    Task<EvolutionSendResult> SendTextAsync(SystemSettings settings, string number, string text,
        CancellationToken ct = default);

    /// <summary>Sends an image with a caption (the alert text goes in the caption).</summary>
    Task<EvolutionSendResult> SendImageAsync(SystemSettings settings, string number, string caption,
        byte[] image, string fileName, CancellationToken ct = default);

    /// <summary>Points the instance's MESSAGES_UPSERT webhook at <paramref name="url"/>; <paramref name="secret"/> travels in the X-Webhook-Key header.</summary>
    Task<EvolutionSendResult> SetWebhookAsync(SystemSettings settings, string url, string secret,
        CancellationToken ct = default);

    Task<EvolutionWebhookState> GetWebhookAsync(SystemSettings settings, CancellationToken ct = default);

    /// <summary>Downloads a received media message (fallback when the webhook did not carry it inline).</summary>
    Task<EvolutionMediaResult> GetMediaBase64Async(SystemSettings settings, string remoteJid, string messageId,
        CancellationToken ct = default);
}
