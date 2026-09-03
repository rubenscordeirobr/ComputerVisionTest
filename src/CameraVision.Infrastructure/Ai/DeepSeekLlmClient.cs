using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CameraVision.Core.Ai;
using CameraVision.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Ai;

/// <summary>DeepSeek through its OpenAI-compatible chat completions endpoint (JSON mode).</summary>
public sealed class DeepSeekLlmClient(IHttpClientFactory httpClientFactory, ILogger<DeepSeekLlmClient> logger) : ILlmClient
{
    private const string Endpoint = "https://api.deepseek.com/chat/completions";

    public AiProvider Provider => AiProvider.DeepSeek;

    public async Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken ct = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("llm");
            client.Timeout = TimeSpan.FromSeconds(20);
            using var http = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey.Trim());
            http.Content = JsonContent.Create(new
            {
                model = request.Model,
                messages = new[]
                {
                    new { role = "system", content = request.SystemPrompt },
                    new { role = "user", content = request.UserText },
                },
                response_format = new { type = "json_object" },
                temperature = 0,
                max_tokens = request.MaxTokens,
            });

            using var response = await client.SendAsync(http, ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
                return new LlmResult(false, Error: $"DeepSeek retornou HTTP {(int)response.StatusCode}: {ErrorMessage(json)}");

            var text = json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
                       choices[0].TryGetProperty("message", out var message) &&
                       message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                ? content.GetString()
                : null;
            return string.IsNullOrWhiteSpace(text)
                ? new LlmResult(false, Error: "DeepSeek não retornou texto.")
                : new LlmResult(true, text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "DeepSeek request failed.");
            return new LlmResult(false, Error: $"Falha ao contatar a API da DeepSeek: {ex.Message}");
        }
    }

    private static string ErrorMessage(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var error) &&
        error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
            ? message.GetString() ?? ""
            : json.ToString();
}
