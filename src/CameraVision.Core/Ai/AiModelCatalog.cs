using CameraVision.Core.Entities;

namespace CameraVision.Core.Ai;

/// <summary>Relative cost/capability of a model within its provider (the caption under its name).</summary>
public enum AiModelTier
{
    Low,
    Medium,
    High,
}

public sealed record AiModelInfo(AiProvider Provider, string Id, string Name, AiModelTier Tier);

/// <summary>The models offered on the AI settings page. Ids are the providers' API model names.</summary>
public static class AiModelCatalog
{
    public static readonly IReadOnlyList<AiModelInfo> All =
    [
        new(AiProvider.Gemini, "gemini-3.5-flash-lite", "Gemini 3.5 Flash-Lite", AiModelTier.Low),
        new(AiProvider.Gemini, "gemini-3.7-flash", "Gemini 3.7 Flash", AiModelTier.Medium),
        new(AiProvider.Gemini, "gemini-3.1-pro", "Gemini 3.1 Pro", AiModelTier.High),

        new(AiProvider.Claude, "claude-haiku-4-5", "Claude Haiku 4.5", AiModelTier.Low),
        new(AiProvider.Claude, "claude-sonnet-5", "Claude Sonnet 5", AiModelTier.Medium),
        new(AiProvider.Claude, "claude-opus-5", "Claude Opus 5", AiModelTier.High),

        new(AiProvider.DeepSeek, "deepseek-v4-flash", "DeepSeek V4 Flash", AiModelTier.Low),
        new(AiProvider.DeepSeek, "deepseek-v4-pro", "DeepSeek V4 Pro", AiModelTier.High),
    ];

    public static IReadOnlyList<AiModelInfo> For(AiProvider provider) =>
        All.Where(m => m.Provider == provider).ToList();

    public static AiModelInfo? Find(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : All.FirstOrDefault(m => m.Id == id.Trim());

    /// <summary>The provider's cheapest model — the default when the provider changes.</summary>
    public static string DefaultModel(AiProvider provider) =>
        For(provider).OrderBy(m => m.Tier).Select(m => m.Id).FirstOrDefault() ?? "";
}
