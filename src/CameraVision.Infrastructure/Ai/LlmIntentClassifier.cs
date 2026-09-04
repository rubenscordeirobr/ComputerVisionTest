using CameraVision.Core.Ai;
using CameraVision.Core.Commands;
using CameraVision.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure.Ai;

/// <summary>
/// Keyword rules first (<see cref="CommandTextRules"/>); when they cannot decide and
/// an AI provider is configured, the model classifies the message into the same
/// fixed intents. The model only ever picks an enum value plus its parameters — its
/// one piece of free text, the "request" summary of an unsupported ask (SPEC-20), is
/// sanitized by <see cref="LlmAnswerParser"/> and never executed as anything else.
/// </summary>
public sealed class LlmIntentClassifier(IEnumerable<ILlmClient> clients, ILogger<LlmIntentClassifier> logger)
    : IIntentClassifier
{
    private const int MaxTextLength = 500;
    private const int MaxAnswerTokens = 400;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<CommandInterpretation> ClassifyAsync(string text, SystemSettings settings, DateTime now,
        ConversationState state, CancellationToken ct = default)
    {
        // A bare "sim"/"não" while an offer is pending answers the offer — checked before
        // the command rules so "não quero" declines instead of disabling the alerts.
        if (state.PendingOffer != null && CommandTextRules.TryMatchConfirmation(text) is { } accepted)
            return new CommandInterpretation(accepted ? CommandIntent.Confirm : CommandIntent.Decline);

        // A tentative rule match ("capturas de pessoas de camisa amarela": a class plus words
        // the rules could not read) stands only when no model is there to look closer.
        var byRules = CommandTextRules.TryMatch(text, now, state.ExpectingDuration);
        if (byRules is { Tentative: false })
            return byRules;

        if (settings.AiProvider == AiProvider.None || string.IsNullOrWhiteSpace(settings.AiApiKey) ||
            string.IsNullOrWhiteSpace(settings.AiModel))
            return byRules ?? CommandInterpretation.Unknown("rules");

        var client = clients.FirstOrDefault(c => c.Provider == settings.AiProvider);
        if (client == null)
        {
            logger.LogWarning("No LLM client registered for provider {Provider}.", settings.AiProvider);
            return byRules ?? CommandInterpretation.Unknown("error");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        var trimmed = text.Length > MaxTextLength ? text[..MaxTextLength] : text;
        LlmResult result;
        try
        {
            result = await client.CompleteJsonAsync(
                new LlmRequest(settings.AiModel.Trim(), settings.AiApiKey, SystemPrompt(now, state), trimmed, MaxAnswerTokens),
                timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("LLM classification timed out after {Timeout}s.", Timeout.TotalSeconds);
            return byRules ?? CommandInterpretation.Unknown("error");
        }

        if (!result.Success || result.Json == null)
        {
            logger.LogWarning("LLM classification failed: {Error}", result.Error);
            return byRules ?? CommandInterpretation.Unknown("error");
        }

        var parsed = LlmAnswerParser.Parse(result.Json, now);
        if (parsed == null)
            logger.LogWarning("LLM classification returned unreadable JSON: {Json}", result.Json);
        // A model that cannot read a message the rules could read loses to the rules.
        if (parsed is null or { Intent: CommandIntent.Unknown } && byRules != null)
            return byRules;
        return parsed ?? CommandInterpretation.Unknown("error");
    }

    private static string SystemPrompt(DateTime now, ConversationState state) =>
        $$"""
         You classify one WhatsApp message (Brazilian Portuguese) sent by a customer of a
         security-camera service to its assistant. The assistant can do exactly four things:
         "enable" — turn on temporary capture alerts for the sender; "disable" — turn them off;
         "camera_status" — report the status of the cameras and of the detection worker;
         "list_captures" — list the latest recorded captures, optionally of one detected object
         class. The detector only knows COCO object classes (person, car, dog, cat, truck...);
         it cannot tell clothing, colours, faces, names, ages or anything else about an object.
         The capture list cannot be filtered by camera, place or time either, and the assistant
         cannot show live video, open gates, add or configure cameras, change rules, delete
         captures or call a person.
         Current local date and time: {{now:yyyy-MM-dd HH:mm}} ({{now:dddd}}).
         {{DurationContext(state)}}{{OfferContext(state)}}
         Reply with a single JSON object and nothing else:
         {"intent": "enable" | "disable" | "set_duration" | "camera_status" | "list_captures" | "unsupported" | "confirm" | "decline" | "unknown",
           "until": "yyyy-MM-ddTHH:mm" | null,
           "until_disabled": true | false,
           "count": integer | null,
           "object_class": "<COCO class name in English, e.g. person, cat, dog, car>" | null,
           "request": "<what the sender wants, in Brazilian Portuguese>" | null,
           "fallback": {"intent": "enable" | "disable" | "camera_status" | "list_captures", "until": ..., "until_disabled": ..., "count": ..., "object_class": ...} | null}
         "until" is the requested end of the alerts when the message states one (resolve
         relative expressions such as "2 horas" or "até as 22h" against the current time);
         "until_disabled" is true when the sender wants them on until further notice.
         "count" and "object_class" only apply to list_captures: the number of captures asked
         for (null when not stated) and the object mentioned (null when none or unknown).
         "unsupported" is a concrete request that goes beyond the four things — a filter that
         cannot be applied (clothing, colour, a specific person, a specific camera or place, a
         time range) or an action the assistant does not have. Then "request" is a short summary of what the sender wants, in Brazilian
         Portuguese, in the infinitive, at most 120 characters (e.g. "ver as últimas capturas de
         pessoas de camisa amarela"), and "fallback" is the closest of the four things that would
         partly help (e.g. list_captures of person with the same count), or null when none
         applies. For every other intent "request" and "fallback" are null.
         Greetings, thanks, questions about anything else, or unclear requests are "unknown".
         Never invent a validity, a count, a class or a request.
         """;

    private static string DurationContext(ConversationState state) => state.ExpectingDuration
        ? "The assistant just asked the sender \"até quando?\" (until when the alerts should stay on); " +
          "a message that only states a validity is intent \"set_duration\".\n"
        : "";

    private static string OfferContext(ConversationState state) => state.PendingOffer is { } offer
        ? $"The assistant just offered to run this command and asked the sender to confirm: {offer.ToJson()}. " +
          "A message that accepts the offer is intent \"confirm\", one that declines it is \"decline\"; " +
          "any other request is classified normally.\n"
        : "";
}
