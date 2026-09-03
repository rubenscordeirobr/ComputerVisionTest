using CameraVision.Core.Entities;

namespace CameraVision.Core.Ai;

/// <summary>One chat completion that must come back as a JSON object.</summary>
public sealed record LlmRequest(string Model, string ApiKey, string SystemPrompt, string UserText, int MaxTokens = 256);

/// <summary>Json is the raw text the model returned (may still carry code fences). Error is PT-BR.</summary>
public sealed record LlmResult(bool Success, string? Json = null, string? Error = null);

/// <summary>
/// Minimal LLM adapter, one per provider. Never throws — failures come back as
/// Error values, like <see cref="IEvolutionApiClient"/>.
/// </summary>
public interface ILlmClient
{
    AiProvider Provider { get; }

    Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken ct = default);
}
