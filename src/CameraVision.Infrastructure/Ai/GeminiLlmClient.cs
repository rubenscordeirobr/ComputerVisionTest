using System.Net.Http.Json;
using System.Text.Json;
using CameraVision.Core.Ai;
using CameraVision.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Ai;

/// <summary>Gemini through the REST generateContent endpoint (JSON response mode).</summary>
public sealed class GeminiLlmClient(IHttpClientFactory httpClientFactory, ILogger<GeminiLlmClient> logger) : ILlmClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    public AiProvider Provider => AiProvider.Gemini;

    public async Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken ct = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("llm");
            client.Timeout = TimeSpan.FromSeconds(20);
            using var http = new HttpRequestMessage(HttpMethod.Post,
                $"{BaseUrl}{Uri.EscapeDataString(request.Model)}:generateContent");
            http.Headers.Add("x-goog-api-key", request.ApiKey.Trim());
            http.Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = request.SystemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = request.UserText } } } },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0,
                    maxOutputTokens = request.MaxTokens,
                },
            });

            using var response = await client.SendAsync(http, ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
                return new LlmResult(false, Error: $"Gemini retornou HTTP {(int)response.StatusCode}: {ErrorMessage(json)}");

            // parts flagged "thought" are the model's reasoning, not the answer.
            var text = "";
            if (json.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                        continue;
                    if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        text += t.GetString();
                }
            }
            return string.IsNullOrWhiteSpace(text)
                ? new LlmResult(false, Error: "Gemini não retornou texto.")
                : new LlmResult(true, text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Gemini request failed.");
            return new LlmResult(false, Error: $"Falha ao contatar a API do Gemini: {ex.Message}");
        }
    }

    private static string ErrorMessage(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var error) &&
        error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
            ? message.GetString() ?? ""
            : json.ToString();
}
