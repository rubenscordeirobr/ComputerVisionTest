using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CameraVision.Core;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Core.WhatsApp;

namespace CameraVision.Api;

/// <summary>
/// Inbound WhatsApp messages from the Evolution API (MESSAGES_UPSERT webhook, SPEC-17).
/// The endpoint only stores a pending WhatsAppCommandLog row — the web app's hosted
/// service interprets and answers it. Voice notes (SPEC-19) arrive decoded in the
/// payload (webhookBase64) and are written under data/inbound-audio for the web app
/// to transcribe. Guarded by the X-Webhook-Key header that the pairing page registers
/// on the instance (the instance's own apikey field in the payload is accepted as a
/// fallback for gateways that drop custom headers). Anything the agent must ignore
/// still gets HTTP 200, otherwise Evolution retries.
/// </summary>
public static class WhatsAppWebhookEndpoints
{
    public const string HeaderName = "X-Webhook-Key";
    private const int MaxTextLength = 1000;
    private const int MaxAudioBytes = 5 * 1024 * 1024;

    public static void MapWhatsAppWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/api/whatsapp/webhook", ReceiveAsync);
    }

    private static async Task<IResult> ReceiveAsync(HttpContext context, ISettingsRepository settingsRepository,
        IWhatsAppCommandRepository commands, StoragePaths storage, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("WhatsAppWebhook");
        var settings = await settingsRepository.GetSystemSettingsAsync(ct);

        JsonElement payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch (JsonException)
        {
            return Results.Ok(new { stored = false, reason = "JSON inválido." });
        }

        if (!IsAuthorized(context, settings, payload))
        {
            logger.LogWarning("WhatsApp webhook call rejected: bad or missing secret.");
            return Results.Unauthorized();
        }

        if (!WhatsAppInboundMessage.TryParse(payload, out var message, out var reason))
            return Results.Ok(new { stored = false, reason });

        var text = message!.Text.Length > MaxTextLength ? message.Text[..MaxTextLength] : message.Text;
        var row = new WhatsAppCommandLog
        {
            MessageId = message.MessageId,
            SenderJid = message.SenderJid,
            SenderNumber = "+" + message.SenderDigits,
            PushName = message.PushName,
            Text = text,
            Kind = message.Kind,
            AudioMimeType = message.AudioMimeType,
            AudioSeconds = message.AudioSeconds,
            MessageAt = message.MessageAt,
            ReceivedAt = DateTime.Now,
        };

        if (message.Kind == WhatsAppMessageKind.Audio && message.AudioBase64 != null)
        {
            try
            {
                row.AudioPath = await SaveAudioAsync(storage, message, ct);
            }
            catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
            {
                // The row still goes in without a file: the web app re-downloads it through Evolution.
                logger.LogWarning(ex, "Could not store the voice note of message {MessageId}.", message.MessageId);
            }
        }

        var stored = await commands.TryAddAsync(row, ct);
        if (stored)
            logger.LogInformation("WhatsApp {Kind} message {MessageId} from {Sender} queued.",
                message.Kind, message.MessageId, message.SenderJid);
        else if (row.AudioPath != null)
            TryDelete(Path.Combine(storage.InboundAudioRoot, row.AudioPath));
        return Results.Ok(new { stored, reason = stored ? null : "Mensagem já recebida." });
    }

    /// <summary>Writes the voice note to {InboundAudioRoot}/{yyyyMMdd}/{id}.ogg; null when it is too large.</summary>
    private static async Task<string?> SaveAudioAsync(StoragePaths storage, WhatsAppInboundMessage message, CancellationToken ct)
    {
        var bytes = Convert.FromBase64String(message.AudioBase64!);
        if (bytes.Length == 0 || bytes.Length > MaxAudioBytes)
            return null;

        var safeId = new string(message.MessageId.Where(char.IsLetterOrDigit).ToArray());
        if (safeId.Length == 0)
            safeId = Guid.NewGuid().ToString("N");
        var relative = Path.Combine(message.MessageAt.ToString("yyyyMMdd"), safeId + ExtensionFor(message.AudioMimeType));
        var fullPath = Path.Combine(storage.InboundAudioRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes, ct);
        return relative.Replace('\\', '/');
    }

    private static string ExtensionFor(string? mimeType)
    {
        var type = (mimeType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        return type switch
        {
            "audio/mp4" or "audio/m4a" or "audio/x-m4a" => ".m4a",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/wav" or "audio/x-wav" => ".wav",
            "audio/webm" => ".webm",
            _ => ".ogg",
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    private static bool IsAuthorized(HttpContext context, SystemSettings settings, JsonElement payload)
    {
        var secret = settings.WhatsAppWebhookSecret?.Trim() ?? "";
        if (secret.Length == 0)
            return false;

        var header = context.Request.Headers[HeaderName].ToString().Trim();
        if (header.Length > 0)
            return FixedTimeEquals(header, secret);

        // Evolution echoes the instance apikey in every payload — only trusted when it
        // matches the key configured here and it is not the compose default.
        var apiKey = settings.EvolutionApiKey?.Trim() ?? "";
        var echoed = payload.ValueKind == JsonValueKind.Object &&
                     payload.TryGetProperty("apikey", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";
        return apiKey.Length >= 16 && echoed.Length > 0 && FixedTimeEquals(echoed, apiKey);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
