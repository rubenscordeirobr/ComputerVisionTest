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
}
