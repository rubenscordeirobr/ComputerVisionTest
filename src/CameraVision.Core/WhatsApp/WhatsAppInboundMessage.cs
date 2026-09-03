using System.Text.Json;
using CameraVision.Core.Entities;

namespace CameraVision.Core.WhatsApp;

/// <summary>
/// A message received by the Evolution instance from one person (never a group): a
/// text, or a voice note (Kind = Audio) whose bytes come inline as base64 when the
/// webhook was registered with webhookBase64 (AudioBase64 null = fetch it later).
/// </summary>
public sealed record WhatsAppInboundMessage(
    string MessageId,
    string SenderJid,
    string SenderDigits,
    string? PushName,
    string Text,
    DateTime MessageAt,
    string? Instance,
    WhatsAppMessageKind Kind = WhatsAppMessageKind.Text,
    string? AudioBase64 = null,
    string? AudioMimeType = null,
    int? AudioSeconds = null)
{
    private const string PersonJidSuffix = "@s.whatsapp.net";

    /// <summary>
    /// Reads an Evolution API MESSAGES_UPSERT payload. False with a PT-BR reason for
    /// anything the agent must not act on: other events, our own messages, groups,
    /// broadcasts, images/videos/stickers/reactions.
    /// </summary>
    public static bool TryParse(JsonElement payload, out WhatsAppInboundMessage? message, out string reason)
    {
        message = null;
        reason = "";

        var evt = GetString(payload, "event")?.Replace('_', '.').ToLowerInvariant();
        if (evt != "messages.upsert")
        {
            reason = $"Evento ignorado ({evt ?? "sem evento"}).";
            return false;
        }
        if (!TryGetObject(payload, "data", out var data) || !TryGetObject(data, "key", out var key))
        {
            reason = "Payload sem data.key.";
            return false;
        }
        if (key.TryGetProperty("fromMe", out var fromMe) && fromMe.ValueKind == JsonValueKind.True)
        {
            reason = "Mensagem enviada pela própria instância.";
            return false;
        }

        var remoteJid = GetString(key, "remoteJid") ?? "";
        var senderJid = remoteJid;
        if (remoteJid.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Mensagem de grupo.";
            return false;
        }
        if (remoteJid.EndsWith("@broadcast", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Lista de transmissão.";
            return false;
        }
        if (!remoteJid.EndsWith(PersonJidSuffix, StringComparison.OrdinalIgnoreCase))
        {
            // "@lid" JIDs hide the phone number; newer Evolution builds add the real one alongside.
            senderJid = GetString(key, "senderPn") ?? GetString(key, "remoteJidAlt") ?? "";
            if (!senderJid.EndsWith(PersonJidSuffix, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Remetente sem número de telefone ({remoteJid}).";
                return false;
            }
        }

        var digits = new string(senderJid[..senderJid.IndexOf('@')].TakeWhile(c => c != ':').Where(char.IsAsciiDigit).ToArray());
        if (digits.Length < 10)
        {
            reason = $"Número do remetente inválido ({senderJid}).";
            return false;
        }

        var messageId = GetString(key, "id");
        if (string.IsNullOrWhiteSpace(messageId))
        {
            reason = "Mensagem sem id.";
            return false;
        }

        var messageAt = DateTime.Now;
        if (data.TryGetProperty("messageTimestamp", out var stamp))
        {
            long seconds = 0;
            if (stamp.ValueKind == JsonValueKind.Number && stamp.TryGetInt64(out var number))
                seconds = number;
            else if (stamp.ValueKind == JsonValueKind.String && long.TryParse(stamp.GetString(), out var parsed))
                seconds = parsed;
            if (seconds > 0)
                messageAt = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
        }

        var pushName = GetString(data, "pushName");
        var instance = GetString(payload, "instance");

        if (!TryGetObject(data, "message", out var body))
        {
            reason = "Mensagem sem conteúdo.";
            return false;
        }

        // Disappearing messages wrap the real content one level down.
        var content = TryGetObject(body, "ephemeralMessage", out var ephemeral) &&
                      TryGetObject(ephemeral, "message", out var inner)
            ? inner
            : body;

        var text = GetString(content, "conversation");
        if (string.IsNullOrWhiteSpace(text) && TryGetObject(content, "extendedTextMessage", out var extended))
            text = GetString(extended, "text");
        if (!string.IsNullOrWhiteSpace(text))
        {
            message = new WhatsAppInboundMessage(messageId.Trim(), senderJid, digits, pushName, text.Trim(),
                messageAt, instance);
            return true;
        }

        if (TryGetObject(content, "audioMessage", out var audio))
        {
            // Evolution puts the decoded file next to the message when webhookBase64 is on.
            var base64 = GetString(body, "base64") ?? GetString(data, "base64");
            int? duration = audio.TryGetProperty("seconds", out var secs) && secs.ValueKind == JsonValueKind.Number &&
                            secs.TryGetInt32(out var s)
                ? s
                : null;
            message = new WhatsAppInboundMessage(messageId.Trim(), senderJid, digits, pushName, "", messageAt, instance,
                WhatsAppMessageKind.Audio, string.IsNullOrWhiteSpace(base64) ? null : base64,
                GetString(audio, "mimetype") ?? "audio/ogg", duration);
            return true;
        }

        reason = "Mensagem sem texto nem áudio (imagem, vídeo, figurinha ou reação).";
        return false;
    }

    private static bool TryGetObject(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value) &&
            value.ValueKind == JsonValueKind.Object)
            return true;
        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
