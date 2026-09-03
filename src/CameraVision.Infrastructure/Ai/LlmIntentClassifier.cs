using System.Globalization;
using System.Text.Json;
using CameraVision.Core.Ai;
using CameraVision.Core.Commands;
using CameraVision.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Ai;

/// <summary>
/// Keyword rules first (<see cref="CommandTextRules"/>); when they cannot decide and
/// an AI provider is configured, the model classifies the message into the same
/// fixed intents. The model only ever picks an enum value and a validity — its text
/// is never executed as anything else.
/// </summary>
public sealed class LlmIntentClassifier(IEnumerable<ILlmClient> clients, ILogger<LlmIntentClassifier> logger)
    : IIntentClassifier
{
    private const int MaxTextLength = 500;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<CommandInterpretation> ClassifyAsync(string text, SystemSettings settings, DateTime now,
        bool expectingDuration, CancellationToken ct = default)
    {
        if (CommandTextRules.TryMatch(text, now, expectingDuration) is { } byRules)
            return byRules;

        if (settings.AiProvider == AiProvider.None || string.IsNullOrWhiteSpace(settings.AiApiKey) ||
            string.IsNullOrWhiteSpace(settings.AiModel))
            return CommandInterpretation.Unknown("rules");

        var client = clients.FirstOrDefault(c => c.Provider == settings.AiProvider);
        if (client == null)
        {
            logger.LogWarning("No LLM client registered for provider {Provider}.", settings.AiProvider);
            return CommandInterpretation.Unknown("error");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        var trimmed = text.Length > MaxTextLength ? text[..MaxTextLength] : text;
        LlmResult result;
        try
        {
            result = await client.CompleteJsonAsync(
                new LlmRequest(settings.AiModel.Trim(), settings.AiApiKey, SystemPrompt(now, expectingDuration), trimmed),
                timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("LLM classification timed out after {Timeout}s.", Timeout.TotalSeconds);
            return CommandInterpretation.Unknown("error");
        }

        if (!result.Success || result.Json == null)
        {
            logger.LogWarning("LLM classification failed: {Error}", result.Error);
            return CommandInterpretation.Unknown("error");
        }

        var parsed = Parse(result.Json, now);
        if (parsed == null)
            logger.LogWarning("LLM classification returned unreadable JSON: {Json}", result.Json);
        return parsed ?? CommandInterpretation.Unknown("error");
    }

    private static string SystemPrompt(DateTime now, bool expectingDuration) =>
        $$"""
         You classify one WhatsApp message (Brazilian Portuguese) sent by a customer of a
         security-camera service to its assistant. The assistant can do four things:
         enable temporary capture alerts for the sender, disable them, report the
         status of the cameras and of the detection worker, or list the latest captures
         (optionally of one object class).
         Current local date and time: {{now:yyyy-MM-dd HH:mm}} ({{now:dddd}}).
         {{(expectingDuration ? "The assistant just asked the sender \"até quando?\" (until when the alerts should stay on); a message that only states a validity is intent \"set_duration\"." : "")}}
         Reply with a single JSON object and nothing else:
         {"intent": "enable" | "disable" | "set_duration" | "camera_status" | "list_captures" | "unknown",
           "until": "yyyy-MM-ddTHH:mm" | null,
           "until_disabled": true | false,
           "count": integer | null,
           "object_class": "<COCO class name in English, e.g. person, cat, dog, car>" | null}
         "until" is the requested end of the alerts when the message states one (resolve
         relative expressions such as "2 horas" or "até as 22h" against the current time);
         "until_disabled" is true when the sender wants them on until further notice.
         "count" and "object_class" only apply to list_captures: the number of captures asked
         for (null when not stated) and the object mentioned (null when none or unknown).
         Greetings, questions about anything else, or unclear requests are "unknown".
         Never invent a validity, a count or a class.
         """;

    /// <summary>Lenient: strips code fences, takes the outermost object, ignores unknown fields.</summary>
    internal static CommandInterpretation? Parse(string raw, DateTime now)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        try
        {
            using var document = JsonDocument.Parse(raw[start..(end + 1)]);
            var root = document.RootElement;
            var intent = root.TryGetProperty("intent", out var i) && i.ValueKind == JsonValueKind.String
                ? i.GetString()?.Trim().ToLowerInvariant()
                : null;
            var kind = intent switch
            {
                "enable" => CommandIntent.EnableAlerts,
                "disable" => CommandIntent.DisableAlerts,
                "set_duration" => CommandIntent.SetDuration,
                "camera_status" => CommandIntent.CameraStatus,
                "list_captures" => CommandIntent.ListCaptures,
                _ => CommandIntent.Unknown,
            };
            if (kind == CommandIntent.Unknown)
                return CommandInterpretation.Unknown("llm");
            if (kind == CommandIntent.CameraStatus)
                return new CommandInterpretation(kind, Source: "llm");
            if (kind == CommandIntent.ListCaptures)
            {
                int? count = root.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number &&
                             c.TryGetInt32(out var n) && n > 0
                    ? n
                    : null;
                var rawClass = root.TryGetProperty("object_class", out var oc) && oc.ValueKind == JsonValueKind.String
                    ? oc.GetString()?.Trim()
                    : null;
                var objectClass = DetectableClassResolver.TryResolve(rawClass);
                var unknownClass = objectClass == null && !string.IsNullOrWhiteSpace(rawClass) ? rawClass : null;
                return new CommandInterpretation(kind, Source: "llm", Count: count, ObjectClass: objectClass,
                    UnknownClass: unknownClass);
            }

            var untilDisabled = root.TryGetProperty("until_disabled", out var u) && u.ValueKind == JsonValueKind.True;
            DateTime? until = null;
            if (!untilDisabled && root.TryGetProperty("until", out var value) && value.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var stamp))
                until = stamp > now ? DateTime.SpecifyKind(stamp, DateTimeKind.Unspecified) : null;

            if (kind == CommandIntent.SetDuration && until == null && !untilDisabled)
                return CommandInterpretation.Unknown("llm");
            return new CommandInterpretation(kind, until, untilDisabled, "llm");
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
