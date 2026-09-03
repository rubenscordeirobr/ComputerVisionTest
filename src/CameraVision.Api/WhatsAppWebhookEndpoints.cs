using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Core.WhatsApp;

namespace CameraVision.Api;

/// <summary>
/// Inbound WhatsApp messages from the Evolution API (MESSAGES_UPSERT webhook, SPEC-17).
/// The endpoint only stores a pending WhatsAppCommandLog row — the web app's hosted
/// service interprets and answers it. Guarded by the X-Webhook-Key header that the
/// pairing page registers on the instance (the instance's own apikey field in the
/// payload is accepted as a fallback for gateways that drop custom headers).
/// Anything the agent must ignore still gets HTTP 200, otherwise Evolution retries.
/// </summary>
public static class WhatsAppWebhookEndpoints
{
    public const string HeaderName = "X-Webhook-Key";
    private const int MaxTextLength = 1000;

    public static void MapWhatsAppWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/api/whatsapp/webhook", ReceiveAsync);
    }

    private static async Task<IResult> ReceiveAsync(HttpContext context, ISettingsRepository settingsRepository,
        IWhatsAppCommandRepository commands, ILoggerFactory loggerFactory, CancellationToken ct)
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
        var stored = await commands.TryAddAsync(new WhatsAppCommandLog
        {
            MessageId = message.MessageId,
            SenderJid = message.SenderJid,
            SenderNumber = "+" + message.SenderDigits,
            PushName = message.PushName,
            Text = text,
            MessageAt = message.MessageAt,
            ReceivedAt = DateTime.Now,
        }, ct);

        if (stored)
            logger.LogInformation("WhatsApp message {MessageId} from {Sender} queued.", message.MessageId, message.SenderJid);
        return Results.Ok(new { stored, reason = stored ? null : "Mensagem já recebida." });
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
