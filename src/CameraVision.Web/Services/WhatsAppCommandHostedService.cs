using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Commands;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Core.WhatsApp;

namespace CameraVision.Web.Services;

/// <summary>
/// The WhatsApp command agent (SPEC-17). Every 2 s it takes the pending rows the API
/// webhook stored, checks the sender is a contact, interprets the text (rules, then
/// the configured AI) and turns "ativar/desativar alertas" into the sender's own
/// temporary notices through <see cref="TemporaryNoticeService"/>. The reply goes out
/// through the Evolution instance and the row keeps the outcome. An "ativar" without a
/// validity is activated for the default hours and the agent asks "até quando?"; the
/// sender's next message within 10 min may then move the end.
/// </summary>
public sealed class WhatsAppCommandHostedService(
    IWhatsAppCommandRepository commands,
    IContactRepository contacts,
    ISettingsRepository settingsRepository,
    IIntentClassifier classifier,
    TemporaryNoticeService notices,
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

        var last = await commands.GetLastProcessedBySenderAsync(row.SenderNumber, ct);
        var expectingDuration = last is { Status: WhatsAppCommandStatus.AwaitingDuration, ProcessedAt: { } at } &&
                                now - at <= FollowUpWindow;

        var interpretation = await classifier.ClassifyAsync(row.Text, settings, now, expectingDuration, ct);
        row.Intent = interpretation.Intent.ToString();
        row.IntentSource = interpretation.Source;

        var status = WhatsAppCommandStatus.Done;
        string reply;
        switch (interpretation.Intent)
        {
            case CommandIntent.EnableAlerts:
            case CommandIntent.SetDuration when !expectingDuration:
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
            default:
                reply = CommandReplyText.Unknown();
                // Keep waiting for the validity when the sender said something unrelated.
                if (expectingDuration)
                    status = WhatsAppCommandStatus.AwaitingDuration;
                break;
        }

        if (interpretation.Intent != CommandIntent.DisableAlerts && interpretation.Intent != CommandIntent.Unknown &&
            !(await settingsRepository.GetAlertSettingsAsync(matches[0].TenantId, AlertChannel.WhatsApp, ct)).Enabled)
            reply += $"\n\n{CommandReplyText.ChannelOffNote}";

        row.ReplyText = reply;
        var sent = await evolution.SendTextAsync(settings, row.SenderNumber, reply, ct);
        if (sent.Success)
        {
            row.Status = status;
            logger.LogInformation("WhatsApp command {Id} from {Sender}: {Intent} ({Source}), {Rules} rule(s).",
                row.Id, row.SenderNumber, row.Intent, row.IntentSource, row.TriggersAffected);
        }
        else
        {
            // The command itself ran; only the confirmation failed.
            row.Status = WhatsAppCommandStatus.Failed;
            row.Detail = Truncate($"Resposta não enviada: {sent.Error}");
            logger.LogWarning("WhatsApp reply to {Sender} failed: {Error}", row.SenderNumber, sent.Error);
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
