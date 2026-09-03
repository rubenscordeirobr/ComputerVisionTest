namespace CameraVision.Core.Entities;

public enum WhatsAppCommandStatus
{
    /// <summary>Stored by the webhook, not yet processed.</summary>
    Pending = 0,

    /// <summary>Answered; the bot asked "até quando?" and waits for the sender's next message.</summary>
    AwaitingDuration = 1,

    Done = 2,
    Ignored = 3,
    Failed = 4,
}

public enum WhatsAppMessageKind
{
    Text = 0,

    /// <summary>A voice note; Text holds the transcript once Whisper has run (SPEC-19).</summary>
    Audio = 1,
}

/// <summary>
/// One inbound WhatsApp message handled by the command agent (SPEC-17): the webhook
/// stores it, the web app's hosted service interprets and answers it. The row is
/// the audit log, the dedupe key (MessageId) and the conversation state.
/// </summary>
public class WhatsAppCommandLog
{
    public int Id { get; set; }

    /// <summary>WhatsApp message id (key.id) — unique, so webhook retries never run a command twice.</summary>
    public string MessageId { get; set; } = "";

    public string SenderJid { get; set; } = "";

    /// <summary>"+" + digits as they appear in the JID (may lack the Brazilian 9th digit).</summary>
    public string SenderNumber { get; set; } = "";

    public string? PushName { get; set; }

    /// <summary>The message text, or the transcript of a voice note (empty until transcribed).</summary>
    public string Text { get; set; } = "";

    public WhatsAppMessageKind Kind { get; set; } = WhatsAppMessageKind.Text;

    /// <summary>Voice note file, relative to the inbound-audio root; null once deleted or when never received inline.</summary>
    public string? AudioPath { get; set; }

    public string? AudioMimeType { get; set; }
    public int? AudioSeconds { get; set; }
    public DateTime MessageAt { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.Now;

    public WhatsAppCommandStatus Status { get; set; } = WhatsAppCommandStatus.Pending;

    /// <summary>PT-BR reason for Ignored/Failed, or a short note for Done.</summary>
    public string? Detail { get; set; }

    public int? TenantId { get; set; }
    public int? ContactId { get; set; }

    /// <summary>Interpreted intent (CommandIntent name) and where it came from ("rules", "llm", "error").</summary>
    public string? Intent { get; set; }

    public string? IntentSource { get; set; }
    public int TriggersAffected { get; set; }
    public string? ReplyText { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
