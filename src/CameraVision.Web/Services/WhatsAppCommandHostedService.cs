using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Commands;
using CameraVision.Core.Entities;
using CameraVision.Core.Health;
using CameraVision.Core.Repositories;
using CameraVision.Core.Speech;
using CameraVision.Core.WhatsApp;
using CameraVision.Infrastructure.Alerts;

namespace CameraVision.Web.Services;

/// <summary>
/// The WhatsApp command agent (SPEC-17). Every 2 s it takes the pending rows the API
/// webhook stored, checks the sender is a contact, interprets the text (rules, then
/// the configured AI) and turns "ativar/desativar alertas" into the sender's own
/// temporary notices through <see cref="TemporaryNoticeService"/>. The reply goes out
/// through the Evolution instance and the row keeps the outcome. An "ativar" without a
/// validity is activated for the default hours and the agent asks "até quando?"; the
/// sender's next message within 10 min may then move the end. "status" and "últimas N
/// capturas" (SPEC-18) are read-only reports composed from the health services and the
/// capture repository. Voice notes (SPEC-19) are transcribed first through
/// <see cref="ISpeechToTextClient"/> and then handled exactly like text, with the
/// transcript quoted at the top of the reply. A request the model understands but the
/// agent cannot serve (SPEC-20) is stored as an <see cref="AgentSuggestion"/> and answered
/// with the closest supported command as an offer, which runs when the sender says "sim".
/// </summary>
public sealed class WhatsAppCommandHostedService(
    IWhatsAppCommandRepository commands,
    IAgentSuggestionRepository suggestions,
    IContactRepository contacts,
    ISettingsRepository settingsRepository,
    IIntentClassifier classifier,
    TemporaryNoticeService notices,
    ICameraRepository cameras,
    ICaptureRepository captures,
    ICameraHealthService cameraHealth,
    IWorkerHealthService workerHealth,
    CaptureLinkService links,
    CaptureAlertComposer composer,
    ISpeechToTextClient speechToText,
    StoragePaths storage,
    IEvolutionApiClient evolution,
    ILogger<WhatsAppCommandHostedService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxCommandsPerMinute = 5;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxMessageAge = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FollowUpWindow = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "WhatsApp command cycle failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var pending = await commands.GetPendingAsync(BatchSize, ct);
        if (pending.Count == 0)
            return;

        var settings = await settingsRepository.GetSystemSettingsAsync(ct);
        foreach (var row in pending)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await ProcessAsync(row, settings, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "WhatsApp command {Id} failed.", row.Id);
                row.Status = WhatsAppCommandStatus.Failed;
                row.Detail = Truncate(ex.Message);
            }
            row.ProcessedAt = DateTime.Now;
            await commands.UpdateAsync(row, ct);
        }
    }

    private async Task ProcessAsync(WhatsAppCommandLog row, SystemSettings settings, CancellationToken ct)
    {
        var now = DateTime.Now;
        if (!settings.WhatsAppBotEnabled)
        {
            Ignore(row, "Agente desativado nas configurações.");
            return;
        }
        if (now - row.MessageAt > MaxMessageAge)
        {
            Ignore(row, "Mensagem antiga (entregue após reconexão).");
            return;
        }

        var matches = await contacts.FindByWhatsAppNumberAsync(
            WhatsAppNumberMatcher.Candidates(row.SenderNumber), ct);
        if (matches.Count == 0)
        {
            Ignore(row, "Número não cadastrado em Contatos.");
            return;
        }
        row.ContactId = matches[0].Id;
        row.TenantId = matches[0].TenantId;

        if (await commands.CountBySenderSinceAsync(row.SenderNumber, now.AddMinutes(-1), ct) > MaxCommandsPerMinute)
        {
            Ignore(row, "Limite de comandos por minuto excedido.");
            return;
        }

        string? heard = null;
        if (row.Kind == WhatsAppMessageKind.Audio)
        {
            heard = await TranscribeAsync(row, settings, ct);
            if (heard == null)
                return; // the row already carries the outcome and the sender was answered
        }

        var state = await StateAsync(row.SenderNumber, now, ct);
        var interpretation = await classifier.ClassifyAsync(row.Text, settings, now, state, ct);
        if (interpretation.Intent == CommandIntent.Confirm)
        {
            // "sim" runs the command offered in the previous reply (SPEC-20); without one
            // there is nothing to confirm.
            interpretation = state.PendingOffer is { } offer
                ? offer with { Source = "offer" }
                : CommandInterpretation.Unknown(interpretation.Source);
        }
        row.Intent = interpretation.Intent.ToString();
        row.IntentSource = interpretation.Source;

        var (reply, status) = await ExecuteAsync(interpretation, row, matches, settings, now, state, ct);

        if ((interpretation.Intent is CommandIntent.EnableAlerts or CommandIntent.SetDuration) &&
            !(await settingsRepository.GetAlertSettingsAsync(matches[0].TenantId, AlertChannel.WhatsApp, ct)).Enabled)
            reply += $"\n\n{CommandReplyText.ChannelOffNote}";

        if (heard != null)
            reply = CommandReplyText.Heard(heard) + reply;
        if (await ReplyAsync(row, settings, reply, status, ct))
            logger.LogInformation("WhatsApp command {Id} from {Sender}: {Intent} ({Source}), {Rules} rule(s).",
                row.Id, row.SenderNumber, row.Intent, row.IntentSource, row.TriggersAffected);
    }

    /// <summary>
    /// The sender's last answered row, within the follow-up window, says what a short
    /// reply means: a validity after "até quando?", or a yes/no to the offered command.
    /// </summary>
    private async Task<ConversationState> StateAsync(string senderNumber, DateTime now, CancellationToken ct)
    {
        var last = await commands.GetLastProcessedBySenderAsync(senderNumber, ct);
        if (last is not { ProcessedAt: { } at } || now - at > FollowUpWindow)
            return ConversationState.None;
        return last.Status switch
        {
            WhatsAppCommandStatus.AwaitingDuration => new ConversationState(ExpectingDuration: true),
            WhatsAppCommandStatus.AwaitingConfirmation =>
                new ConversationState(PendingOffer: CommandInterpretation.TryFromJson(last.FollowUpJson)),
            _ => ConversationState.None,
        };
    }

    /// <summary>
    /// Runs the interpreted command for every contact the number matched and composes the
    /// reply; the row keeps the outcome (TriggersAffected, Detail, a pending offer). Shared
    /// by the direct path and by a confirmed offer.
    /// </summary>
    private async Task<(string Reply, WhatsAppCommandStatus Status)> ExecuteAsync(
        CommandInterpretation interpretation, WhatsAppCommandLog row, IReadOnlyList<Contact> matches,
        SystemSettings settings, DateTime now, ConversationState state, CancellationToken ct)
    {
        var status = WhatsAppCommandStatus.Done;
        string reply;
        switch (interpretation.Intent)
        {
            case CommandIntent.EnableAlerts:
            case CommandIntent.SetDuration when !state.ExpectingDuration:
            {
                var askUntilWhen = !interpretation.HasDuration;
                var expiresAt = askUntilWhen
                    ? now.AddHours(DefaultHours(settings))
                    : interpretation.UntilDisabled ? null : interpretation.Until;

                var created = 0;
                var extended = 0;
                DateTime? effectiveExpiry = null;
                foreach (var contact in matches)
                {
                    var result = await notices.ActivateForContactAsync(contact.TenantId, contact.Id,
                        AlertChannel.WhatsApp, now, expiresAt, ct);
                    created += result.Created;
                    extended += result.Extended;
                    effectiveExpiry = result.ExpiresAt;
                }
                row.TriggersAffected = created + extended;
                if (row.TriggersAffected == 0)
                {
                    reply = CommandReplyText.NoRules();
                    break;
                }
                reply = CommandReplyText.Enabled(row.TriggersAffected, extended, effectiveExpiry, askUntilWhen);
                if (askUntilWhen)
                    status = WhatsAppCommandStatus.AwaitingDuration;
                break;
            }
            case CommandIntent.SetDuration:
            {
                var expiresAt = interpretation.UntilDisabled ? null : interpretation.Until;
                var updated = 0;
                DateTime? effectiveExpiry = null;
                foreach (var contact in matches)
                {
                    updated += await notices.SetExpiryForContactAsync(contact.TenantId, contact.Id,
                        AlertChannel.WhatsApp, now, expiresAt, ct);
                    effectiveExpiry = TemporaryNoticeService.Clamp(now, expiresAt);
                }
                if (updated == 0)
                {
                    // The notice ended meanwhile — honour the answer by starting a new one.
                    foreach (var contact in matches)
                    {
                        var result = await notices.ActivateForContactAsync(contact.TenantId, contact.Id,
                            AlertChannel.WhatsApp, now, expiresAt, ct);
                        updated += result.Rules;
                        effectiveExpiry = result.ExpiresAt;
                    }
                }
                row.TriggersAffected = updated;
                reply = updated == 0 ? CommandReplyText.NoRules() : CommandReplyText.DurationSet(updated, effectiveExpiry);
                break;
            }
            case CommandIntent.DisableAlerts:
            {
                var ended = 0;
                foreach (var contact in matches)
                    ended += await notices.EndForContactAsync(contact.TenantId, contact.Id, AlertChannel.WhatsApp, now, ct);
                row.TriggersAffected = ended;
                reply = CommandReplyText.Disabled(ended);
                break;
            }
            case CommandIntent.CameraStatus:
            {
                var lines = new List<CameraStatusLine>();
                foreach (var contact in matches)
                {
                    foreach (var camera in await cameras.GetAllAsync(contact.TenantId, ct))
                    {
                        var health = cameraHealth.TryGet(camera.Id);
                        lines.Add(new CameraStatusLine(camera.Name, camera.Enabled,
                            !string.IsNullOrWhiteSpace(camera.StreamUrl) || !string.IsNullOrWhiteSpace(camera.IpAddress),
                            health?.Status, health?.PingMs ?? health?.ConnectMs, camera.ProcessorStatusAt));
                    }
                }
                reply = CameraStatusReport.Compose(workerHealth.Current, lines, now);
                row.Detail = $"{lines.Count} câmera(s)";
                break;
            }
            case CommandIntent.ListCaptures:
            {
                if (interpretation.UnknownClass != null)
                {
                    reply = CaptureListReport.UnknownClass(interpretation.UnknownClass);
                    row.Detail = $"Objeto desconhecido: {interpretation.UnknownClass}";
                    break;
                }

                var requested = interpretation.Count ?? CommandInterpretation.DefaultCount;
                var take = Math.Clamp(requested, 1, CommandInterpretation.MaxCount);
                var items = new List<Capture>();
                var total = 0;
                foreach (var contact in matches)
                {
                    var page = await captures.QueryAsync(new CaptureFilter
                    {
                        TenantId = contact.TenantId,
                        ObjectClass = interpretation.ObjectClass,
                        Take = take,
                    }, ct);
                    items.AddRange(page.Items);
                    total += page.TotalCount;
                }
                var latest = items.OrderByDescending(c => c.StartedAt).ThenByDescending(c => c.Id).Take(take).ToList();
                var baseUrl = composer.ResolveBaseUrl(settings, out _);
                reply = CaptureListReport.Compose(latest, total, requested, interpretation.ObjectClass,
                    c => links.PlaybackUrl(c.Id, baseUrl));
                row.Detail = $"{latest.Count} captura(s)";
                break;
            }
            case CommandIntent.Unsupported:
            {
                // Understood but not a capability: keep it for the SuperAdmin and, when the
                // model found a close enough command, offer it and wait for "sim" (SPEC-20).
                await suggestions.AddAsync(new AgentSuggestion
                {
                    TenantId = matches[0].TenantId,
                    ContactId = matches[0].Id,
                    CommandLogId = row.Id,
                    SenderNumber = row.SenderNumber,
                    PushName = row.PushName,
                    MessageText = row.Text,
                    Request = interpretation.Request ?? "",
                    Model = string.IsNullOrWhiteSpace(settings.AiModel) ? null : settings.AiModel.Trim(),
                    CreatedAt = now,
                }, ct);
                var offer = CommandReplyText.Offer(interpretation.Fallback);
                if (offer != null)
                {
                    row.FollowUpJson = interpretation.Fallback!.ToJson();
                    status = WhatsAppCommandStatus.AwaitingConfirmation;
                }
                row.Detail = offer == null ? "Sugestão registrada" : "Sugestão registrada · alternativa oferecida";
                reply = CommandReplyText.Unsupported(interpretation.Request ?? "", offer);
                break;
            }
            case CommandIntent.Decline:
                row.Detail = "Oferta recusada";
                reply = CommandReplyText.Declined();
                break;
            default:
                reply = CommandReplyText.Unknown();
                // Keep waiting for the validity or the yes/no when the sender said something unrelated.
                if (state.ExpectingDuration)
                {
                    status = WhatsAppCommandStatus.AwaitingDuration;
                }
                else if (state.PendingOffer is { } pending)
                {
                    row.FollowUpJson = pending.ToJson();
                    status = WhatsAppCommandStatus.AwaitingConfirmation;
                }
                break;
        }
        return (reply, status);
    }

    /// <summary>Sends the reply and marks the row; false when Evolution refused (the command itself already ran).</summary>
    private async Task<bool> ReplyAsync(WhatsAppCommandLog row, SystemSettings settings, string reply,
        WhatsAppCommandStatus status, CancellationToken ct)
    {
        row.ReplyText = reply;
        var sent = await evolution.SendTextAsync(settings, row.SenderNumber, reply, ct);
        if (sent.Success)
        {
            row.Status = status;
            return true;
        }
        row.Status = WhatsAppCommandStatus.Failed;
        row.Detail = Truncate($"Resposta não enviada: {sent.Error}");
        logger.LogWarning("WhatsApp reply to {Sender} failed: {Error}", row.SenderNumber, sent.Error);
        return false;
    }

    /// <summary>
    /// Turns the voice note into row.Text. Null means the message was answered here
    /// (audio disabled, too long, download or transcription failure) and processing
    /// stops. The audio file is deleted either way — only the text is kept.
    /// </summary>
    private async Task<string?> TranscribeAsync(WhatsAppCommandLog row, SystemSettings settings, CancellationToken ct)
    {
        var fullPath = row.AudioPath == null ? null : Path.Combine(storage.InboundAudioRoot, row.AudioPath);
        try
        {
            if (!settings.WhatsAppAudioEnabled)
            {
                row.Detail = "Comandos por áudio desativados.";
                await ReplyAsync(row, settings, CommandReplyText.AudioDisabled(), WhatsAppCommandStatus.Done, ct);
                return null;
            }

            var maxSeconds = Math.Max(5, settings.WhatsAppAudioMaxSeconds);
            if (row.AudioSeconds is { } seconds && seconds > maxSeconds)
            {
                row.Detail = $"Áudio de {seconds} s excede o máximo de {maxSeconds} s.";
                await ReplyAsync(row, settings, CommandReplyText.AudioTooLong(maxSeconds), WhatsAppCommandStatus.Done, ct);
                return null;
            }

            byte[]? audio = null;
            var mimeType = row.AudioMimeType ?? "audio/ogg";
            if (fullPath != null && File.Exists(fullPath))
            {
                audio = await File.ReadAllBytesAsync(fullPath, ct);
            }
            else
            {
                var media = await evolution.GetMediaBase64Async(settings, row.SenderJid, row.MessageId, ct);
                if (media.Success)
                {
                    audio = media.Bytes;
                    mimeType = media.MimeType ?? mimeType;
                }
                else
                {
                    row.Detail = Truncate($"Áudio não obtido: {media.Error}");
                }
            }
            if (audio == null || audio.Length == 0)
            {
                await ReplyAsync(row, settings, CommandReplyText.AudioNotUnderstood(), WhatsAppCommandStatus.Failed, ct);
                row.Status = WhatsAppCommandStatus.Failed;
                return null;
            }

            var started = DateTime.Now;
            var result = await speechToText.TranscribeAsync(settings,
                new SpeechToTextRequest(audio, mimeType, "voice" + (Path.GetExtension(fullPath) is { Length: > 0 } ext ? ext : ".ogg"),
                    string.IsNullOrWhiteSpace(settings.WhisperLanguage) ? null : settings.WhisperLanguage), ct);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                row.Detail = Truncate(result.Error ?? "Nenhuma fala reconhecida no áudio.");
                logger.LogWarning("Voice note {Id} from {Sender} not transcribed: {Error}", row.Id, row.SenderNumber, row.Detail);
                await ReplyAsync(row, settings, CommandReplyText.AudioNotUnderstood(), WhatsAppCommandStatus.Failed, ct);
                row.Status = WhatsAppCommandStatus.Failed;
                return null;
            }

            var transcript = result.Text.Length > 1000 ? result.Text[..1000] : result.Text;
            row.Text = transcript;
            row.Detail = $"Transcrito em {(DateTime.Now - started).TotalSeconds:0.0} s";
            return transcript;
        }
        finally
        {
            if (fullPath != null)
            {
                try
                {
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                    row.AudioPath = null;
                }
                catch (IOException ex)
                {
                    logger.LogWarning(ex, "Could not delete voice note {Path}.", fullPath);
                }
            }
        }
    }

    /// <summary>Rows upgraded before the column existed carry 0 — that means the shipped default.</summary>
    private static int DefaultHours(SystemSettings settings) =>
        settings.WhatsAppBotDefaultHours is >= 1 and <= 72 ? settings.WhatsAppBotDefaultHours : 8;

    private static void Ignore(WhatsAppCommandLog row, string reason)
    {
        row.Status = WhatsAppCommandStatus.Ignored;
        row.Detail = reason;
    }

    private static string Truncate(string text) => text.Length > 500 ? text[..500] : text;
}
