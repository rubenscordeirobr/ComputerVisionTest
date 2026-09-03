using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using CameraVision.Core.Ai;
using CameraVision.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Ai;

/// <summary>Claude through the official Anthropic SDK. One short, low-effort call per message.</summary>
public sealed class ClaudeLlmClient(ILogger<ClaudeLlmClient> logger) : ILlmClient
{
    public AiProvider Provider => AiProvider.Claude;

    public async Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken ct = default)
    {
        try
        {
            var client = new AnthropicClient { ApiKey = request.ApiKey.Trim() };
            // Effort exists on the Opus/Sonnet line; Haiku 4.5 rejects it.
            var supportsEffort = !request.Model.Contains("haiku", StringComparison.OrdinalIgnoreCase);
            var parameters = new MessageCreateParams
            {
                Model = request.Model,
                MaxTokens = request.MaxTokens,
                System = request.SystemPrompt,
                Messages = [new() { Role = Role.User, Content = request.UserText }],
                OutputConfig = supportsEffort ? new OutputConfig { Effort = Effort.Low } : null,
            };

            var response = await client.Messages.Create(parameters, cancellationToken: ct);
            var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
            return string.IsNullOrWhiteSpace(text)
                ? new LlmResult(false, Error: $"O modelo não retornou texto (stop_reason: {response.StopReason}).")
                : new LlmResult(true, text);
        }
        catch (AnthropicApiException ex)
        {
            logger.LogWarning(ex, "Claude request failed.");
            return new LlmResult(false, Error: $"Claude retornou um erro: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Claude request failed.");
            return new LlmResult(false, Error: $"Falha ao contatar a API da Anthropic: {ex.Message}");
        }
    }
}
