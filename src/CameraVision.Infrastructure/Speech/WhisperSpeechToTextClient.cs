using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CameraVision.Core.Entities;
using CameraVision.Core.Speech;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Speech;

/// <summary>
/// Whisper server (hwdsl2/whisper-server or any OpenAI-compatible
/// POST /v1/audio/transcriptions). An empty transcript is a success with empty Text —
/// the caller decides what to tell the sender.
/// </summary>
public sealed class WhisperSpeechToTextClient(IHttpClientFactory httpClientFactory, ILogger<WhisperSpeechToTextClient> logger)
    : ISpeechToTextClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public async Task<SpeechToTextResult> TranscribeAsync(SystemSettings settings, SpeechToTextRequest request,
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(settings.WhisperBaseUrl?.Trim(), UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https"))
            return new SpeechToTextResult(false, Error: "Configure a URL do servidor Whisper em Sistema → Inteligência artificial.");

        try
        {
            using var client = httpClientFactory.CreateClient("whisper");
            client.Timeout = Timeout;
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(request.Audio);
            file.Headers.ContentType = MediaTypeHeaderValue.TryParse(request.MimeType, out var mime)
                ? mime
                : new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "file", request.FileName);
            form.Add(new StringContent("whisper-1"), "model");
            form.Add(new StringContent("json"), "response_format");
            if (!string.IsNullOrWhiteSpace(request.Language))
                form.Add(new StringContent(request.Language.Trim()), "language");

            using var http = new HttpRequestMessage(HttpMethod.Post,
                new Uri(baseUri.ToString().TrimEnd('/') + "/v1/audio/transcriptions"));
            if (!string.IsNullOrWhiteSpace(settings.WhisperApiKey))
                http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.WhisperApiKey.Trim());
            http.Content = form;

            using var response = await client.SendAsync(http, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return new SpeechToTextResult(false, Error:
                    $"O servidor Whisper retornou HTTP {(int)response.StatusCode}: {Truncate(body)}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var text = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("text", out var t) &&
                       t.ValueKind == JsonValueKind.String
                ? t.GetString()?.Trim() ?? ""
                : "";
            return new SpeechToTextResult(true, text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Whisper transcription failed.");
            return new SpeechToTextResult(false, Error: $"Falha ao contatar o servidor Whisper: {ex.Message}");
        }
    }

    private static string Truncate(string text) => text.Length > 200 ? text[..200] : text;
}
