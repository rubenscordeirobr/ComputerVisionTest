namespace CameraVision.Core.Commands;

/// <summary>What the sender of a WhatsApp message asked the agent to do (SPEC-17).</summary>
public enum CommandIntent
{
    Unknown = 0,

    /// <summary>Start a temporary notice on the sender's own number.</summary>
    EnableAlerts = 1,

    /// <summary>End the sender's running temporary notices.</summary>
    DisableAlerts = 2,

    /// <summary>Only a validity ("2 horas", "até 22:00") — the answer to the agent's "até quando?".</summary>
    SetDuration = 3,
}

/// <summary>
/// Interpreted message. Until/UntilDisabled carry the requested validity when the
/// text had one; Source says who decided ("rules", "llm", "error").
/// </summary>
public sealed record CommandInterpretation(
    CommandIntent Intent,
    DateTime? Until = null,
    bool UntilDisabled = false,
    string Source = "rules")
{
    public bool HasDuration => Until != null || UntilDisabled;

    public static CommandInterpretation Unknown(string source) => new(CommandIntent.Unknown, Source: source);
}
