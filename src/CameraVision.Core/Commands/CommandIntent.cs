namespace CameraVision.Core.Commands;

/// <summary>What the sender of a WhatsApp message asked the agent to do (SPEC-17/18).</summary>
public enum CommandIntent
{
    Unknown = 0,

    /// <summary>Start a temporary notice on the sender's own number.</summary>
    EnableAlerts = 1,

    /// <summary>End the sender's running temporary notices.</summary>
    DisableAlerts = 2,

    /// <summary>Only a validity ("2 horas", "até 22:00") — the answer to the agent's "até quando?".</summary>
    SetDuration = 3,

    /// <summary>Camera health and detection-worker liveness report (read-only).</summary>
    CameraStatus = 4,

    /// <summary>The latest captures, optionally of one object class (read-only).</summary>
    ListCaptures = 5,
}

/// <summary>
/// Interpreted message. Until/UntilDisabled carry the requested validity when the
/// text had one; Count/ObjectClass the list request; UnknownClass the object word
/// the sender used when it matched no detectable class. Source says who decided
/// ("rules", "llm", "error").
/// </summary>
public sealed record CommandInterpretation(
    CommandIntent Intent,
    DateTime? Until = null,
    bool UntilDisabled = false,
    string Source = "rules",
    int? Count = null,
    string? ObjectClass = null,
    string? UnknownClass = null)
{
    /// <summary>Captures listed when the message gives no number, and the most it may ask for.</summary>
    public const int DefaultCount = 5;

    public const int MaxCount = 10;

    public bool HasDuration => Until != null || UntilDisabled;

    /// <summary>True when the sender asked for a read-only report (no trigger changes).</summary>
    public bool IsReadOnly => Intent is CommandIntent.CameraStatus or CommandIntent.ListCaptures;

    public static CommandInterpretation Unknown(string source) => new(CommandIntent.Unknown, Source: source);
}
