using System.Globalization;
using System.Text.Json;

namespace CameraVision.Core.Commands;

/// <summary>
/// Reads the JSON object the LLM answers with (SPEC-17/18/20). Lenient: strips code
/// fences, takes the outermost object, ignores unknown fields. The model only ever picks
/// an intent plus its parameters — the one piece of free text kept is the short
/// "request" summary of an unsupported ask, and it is sanitized on the way in.
/// </summary>
public static class LlmAnswerParser
{
    private const string Source = "llm";

    public static CommandInterpretation? Parse(string raw, DateTime now)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        try
        {
            using var document = JsonDocument.Parse(raw[start..(end + 1)]);
            return ParseObject(document.RootElement, now, topLevel: true);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The top-level answer, or the "fallback" object — which may only be a runnable command.</summary>
    private static CommandInterpretation? ParseObject(JsonElement root, DateTime now, bool topLevel)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var kind = String(root, "intent")?.Trim().ToLowerInvariant() switch
        {
            "enable" => CommandIntent.EnableAlerts,
            "disable" => CommandIntent.DisableAlerts,
            "set_duration" => CommandIntent.SetDuration,
            "camera_status" => CommandIntent.CameraStatus,
            "list_captures" => CommandIntent.ListCaptures,
            "unsupported" => CommandIntent.Unsupported,
            "confirm" => CommandIntent.Confirm,
            "decline" => CommandIntent.Decline,
            _ => CommandIntent.Unknown,
        };

        switch (kind)
        {
            case CommandIntent.Unknown:
                return topLevel ? CommandInterpretation.Unknown(Source) : null;
            case CommandIntent.Confirm:
            case CommandIntent.Decline:
                return topLevel ? new CommandInterpretation(kind, Source: Source) : null;
            case CommandIntent.Unsupported:
            {
                if (!topLevel)
                    return null;
                var fallback = root.TryGetProperty("fallback", out var f) ? ParseObject(f, now, topLevel: false) : null;
                return CommandInterpretation.Unsupported(String(root, "request"), Source, fallback);
            }
            case CommandIntent.CameraStatus:
                return new CommandInterpretation(kind, Source: Source);
            case CommandIntent.ListCaptures:
            {
                int? count = root.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number &&
                             c.TryGetInt32(out var n) && n > 0
                    ? n
                    : null;
                var rawClass = String(root, "object_class")?.Trim();
                var objectClass = DetectableClassResolver.TryResolve(rawClass);
                var unknownClass = objectClass == null && !string.IsNullOrWhiteSpace(rawClass) ? rawClass : null;
                return new CommandInterpretation(kind, Source: Source, Count: count, ObjectClass: objectClass,
                    UnknownClass: unknownClass);
            }
        }

        if (!topLevel && kind == CommandIntent.SetDuration)
            return null;

        var untilDisabled = root.TryGetProperty("until_disabled", out var u) && u.ValueKind == JsonValueKind.True;
        DateTime? until = null;
        if (!untilDisabled && String(root, "until") is { } value &&
            DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var stamp))
            until = stamp > now ? DateTime.SpecifyKind(stamp, DateTimeKind.Unspecified) : null;

        if (kind == CommandIntent.SetDuration && until == null && !untilDisabled)
            return CommandInterpretation.Unknown(Source);
        return new CommandInterpretation(kind, until, untilDisabled, Source);
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
