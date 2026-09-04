using System.Text.Json;
using System.Text.Json.Serialization;

namespace CameraVision.Core.Commands;

/// <summary>What the sender of a WhatsApp message asked the agent to do (SPEC-17/18/20).</summary>
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

    /// <summary>Understood, but not something the agent can do — recorded as a suggestion (SPEC-20).</summary>
    Unsupported = 6,

    /// <summary>"sim" to the agent's offer of the closest supported command.</summary>
    Confirm = 7,

    /// <summary>"não" to that offer.</summary>
    Decline = 8,
}

/// <summary>
/// Interpreted message. Until/UntilDisabled carry the requested validity when the
/// text had one; Count/ObjectClass the list request; UnknownClass the object word
/// the sender used when it matched no detectable class. Request is the model's short
/// PT-BR summary of an unsupported ask and Fallback the closest supported command it
/// proposed instead. Source says who decided ("rules", "llm", "error", "offer").
/// Tentative marks a rule match that left part of the message unread ("capturas de
/// pessoas de camisa amarela") — a model, when configured, gets to look first.
/// </summary>
public sealed record CommandInterpretation(
    CommandIntent Intent,
    DateTime? Until = null,
    bool UntilDisabled = false,
    string Source = "rules",
    int? Count = null,
    string? ObjectClass = null,
    string? UnknownClass = null,
    string? Request = null,
    CommandInterpretation? Fallback = null,
    bool Tentative = false)
{
    /// <summary>Captures listed when the message gives no number, and the most it may ask for.</summary>
    public const int DefaultCount = 5;

    public const int MaxCount = 10;

    /// <summary>Longest "what the sender wants" summary kept from the model.</summary>
    public const int MaxRequestLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IgnoreReadOnlyProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool HasDuration => Until != null || UntilDisabled;

    /// <summary>True when the sender asked for a read-only report (no trigger changes).</summary>
    public bool IsReadOnly => Intent is CommandIntent.CameraStatus or CommandIntent.ListCaptures;

    /// <summary>True for the intents the agent can run by itself — the only ones worth offering.</summary>
    public bool IsExecutable => Intent is CommandIntent.EnableAlerts or CommandIntent.DisableAlerts
        or CommandIntent.CameraStatus or CommandIntent.ListCaptures;

    public static CommandInterpretation Unknown(string source) => new(CommandIntent.Unknown, Source: source);

    /// <summary>
    /// Understood but not implemented. The request is model text, so it is folded to one
    /// line, unquoted and capped at <see cref="MaxRequestLength"/>; empty → Unknown. The
    /// fallback is kept only when it is something the agent can actually run.
    /// </summary>
    public static CommandInterpretation Unsupported(string? request, string source, CommandInterpretation? fallback = null)
    {
        var cleaned = CleanRequest(request);
        if (cleaned == null)
            return Unknown(source);
        var offer = fallback is { IsExecutable: true }
            ? fallback with { Request = null, Fallback = null, Tentative = false }
            : null;
        return new CommandInterpretation(CommandIntent.Unsupported, Source: source, Request: cleaned, Fallback: offer);
    }

    /// <summary>Serialized form kept on the command log while an offer waits for "sim"/"não".</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static CommandInterpretation? TryFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<CommandInterpretation>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? CleanRequest(string? request)
    {
        if (string.IsNullOrWhiteSpace(request))
            return null;
        var oneLine = string.Join(' ', request.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim('"', '\'', '“', '”', '.', ' ');
        if (oneLine.Length == 0)
            return null;
        return oneLine.Length > MaxRequestLength ? oneLine[..(MaxRequestLength - 1)].TrimEnd() + "…" : oneLine;
    }
}
