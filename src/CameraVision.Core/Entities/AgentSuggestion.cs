namespace CameraVision.Core.Entities;

/// <summary>
/// A WhatsApp request the agent understood but cannot fulfil (SPEC-20): "últimas
/// capturas de pessoas de camisa amarela", "abre o portão". Written by the command
/// agent from the LLM's summary, reviewed by the SuperAdmin on /system/suggestions.
/// </summary>
public class AgentSuggestion
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? ContactId { get; set; }

    /// <summary>The WhatsAppCommandLog row that carried the request.</summary>
    public int? CommandLogId { get; set; }

    public string SenderNumber { get; set; } = "";
    public string? PushName { get; set; }

    /// <summary>The message as received (or the voice-note transcript).</summary>
    public string MessageText { get; set; } = "";

    /// <summary>The model's one-line PT-BR summary of what the sender wants.</summary>
    public string Request { get; set; } = "";

    /// <summary>Model id that produced the summary (SystemSettings.AiModel at the time).</summary>
    public string? Model { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Null until the SuperAdmin marks it as seen.</summary>
    public DateTime? ReviewedAt { get; set; }
}
