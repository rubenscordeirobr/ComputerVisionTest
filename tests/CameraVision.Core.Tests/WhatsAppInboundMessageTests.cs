using System.Text.Json;
using CameraVision.Core.WhatsApp;

namespace CameraVision.Core.Tests;

public class WhatsAppInboundMessageTests
{
    private static JsonElement Payload(string remoteJid = "5549988887777@s.whatsapp.net", bool fromMe = false,
        string? conversation = "ativar alertas", string? extendedText = null, string evt = "messages.upsert",
        string? id = "3EB0ABC123") =>
        JsonSerializer.SerializeToElement(new
        {
            @event = evt,
            instance = "CameraVision",
            data = new
            {
                key = new { remoteJid, fromMe, id },
                pushName = "Rubens",
                message = extendedText == null
                    ? (object)new { conversation }
                    : new { extendedTextMessage = new { text = extendedText } },
                messageType = extendedText == null ? "conversation" : "extendedTextMessage",
                messageTimestamp = 1788000000,
            },
            sender = "5549900000000@s.whatsapp.net",
            apikey = "key",
        });

    [Fact]
    public void Parses_a_direct_text_message()
    {
        Assert.True(WhatsAppInboundMessage.TryParse(Payload(), out var message, out _));
        Assert.NotNull(message);
        Assert.Equal("3EB0ABC123", message.MessageId);
        Assert.Equal("5549988887777", message.SenderDigits);
        Assert.Equal("Rubens", message.PushName);
        Assert.Equal("ativar alertas", message.Text);
        Assert.Equal("CameraVision", message.Instance);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1788000000).LocalDateTime, message.MessageAt);
    }

    [Fact]
    public void Event_name_may_use_underscores_and_upper_case() =>
        Assert.True(WhatsAppInboundMessage.TryParse(Payload(evt: "MESSAGES_UPSERT"), out _, out _));

    [Fact]
    public void Extended_text_is_picked_up()
    {
        Assert.True(WhatsAppInboundMessage.TryParse(Payload(conversation: null, extendedText: "desativar"), out var message, out _));
        Assert.Equal("desativar", message!.Text);
    }

    [Fact]
    public void Own_messages_are_rejected()
    {
        Assert.False(WhatsAppInboundMessage.TryParse(Payload(fromMe: true), out _, out var reason));
        Assert.Contains("própria", reason);
    }

    [Fact]
    public void Group_messages_are_rejected()
    {
        Assert.False(WhatsAppInboundMessage.TryParse(Payload(remoteJid: "120363000000@g.us"), out _, out var reason));
        Assert.Contains("grupo", reason);
    }

    [Fact]
    public void Media_without_text_is_rejected() =>
        Assert.False(WhatsAppInboundMessage.TryParse(Payload(conversation: null), out _, out _));

    [Fact]
    public void Other_events_are_rejected() =>
        Assert.False(WhatsAppInboundMessage.TryParse(Payload(evt: "connection.update"), out _, out _));

    [Fact]
    public void Missing_id_is_rejected() =>
        Assert.False(WhatsAppInboundMessage.TryParse(Payload(id: null), out _, out _));

    [Fact]
    public void Lid_jid_without_phone_is_rejected() =>
        Assert.False(WhatsAppInboundMessage.TryParse(Payload(remoteJid: "123456789@lid"), out _, out _));
}
