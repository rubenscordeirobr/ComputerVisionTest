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

/// <summary>
/// Minimal Evolution API client for the WhatsApp pairing flow (connect → QR → state).
/// Never throws — failures come back as Error values.
/// </summary>
public interface IEvolutionApiClient
{
    Task<EvolutionQr> ConnectAsync(SystemSettings settings, CancellationToken ct = default);
    Task<EvolutionState> GetStateAsync(SystemSettings settings, CancellationToken ct = default);
}
