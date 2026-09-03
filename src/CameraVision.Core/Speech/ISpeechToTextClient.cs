using CameraVision.Core.Entities;

namespace CameraVision.Core.Speech;

/// <summary>One audio file to transcribe. Language is a BCP-47 code ("pt") or null for autodetect.</summary>
public sealed record SpeechToTextRequest(byte[] Audio, string MimeType, string FileName, string? Language);

/// <summary>Text is the trimmed transcript; Error is PT-BR, suitable for logs and the command log.</summary>
public sealed record SpeechToTextResult(bool Success, string? Text = null, string? Error = null);

/// <summary>
/// Speech-to-text for WhatsApp voice commands (SPEC-19). Implementations never throw —
/// failures come back as Error values, like <see cref="IEvolutionApiClient"/>.
/// </summary>
public interface ISpeechToTextClient
{
    Task<SpeechToTextResult> TranscribeAsync(SystemSettings settings, SpeechToTextRequest request,
        CancellationToken ct = default);
}
