using System.Text.Json;

namespace CameraVision.Core.WhatsApp;

/// <summary>A text message received by the Evolution instance from one person (never a group).</summary>
public sealed record WhatsAppInboundMessage(
    string MessageId,
    string SenderJid,
    string SenderDigits,
    string? PushName,
    string Text,
    DateTime MessageAt,
    string? Instance)
{
    private const string PersonJidSuffix = "@s.whatsapp.net";

    /// <summary>
    /// Reads an Evolution API MESSAGES_UPSERT payload. False with a PT-BR reason for
    /// anything the agent must not act on: other events, our own messages, groups,
    /// broadcasts, non-text messages.
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

        string? text = null;
        if (TryGetObject(data, "message", out var body))
        {
            text = GetString(body, "conversation");
            if (string.IsNullOrWhiteSpace(text) && TryGetObject(body, "extendedTextMessage", out var extended))
                text = GetString(extended, "text");
            if (string.IsNullOrWhiteSpace(text) && TryGetObject(body, "ephemeralMessage", out var ephemeral) &&
                TryGetObject(ephemeral, "message", out var inner))
            {
                text = GetString(inner, "conversation");
                if (string.IsNullOrWhiteSpace(text) && TryGetObject(inner, "extendedTextMessage", out var innerExtended))
                    text = GetString(innerExtended, "text");
            }
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            reason = "Mensagem sem texto (mídia, áudio ou reação).";
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

        message = new WhatsAppInboundMessage(messageId.Trim(), senderJid, digits, GetString(data, "pushName"),
            text.Trim(), messageAt, GetString(payload, "instance"));
        return true;
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
