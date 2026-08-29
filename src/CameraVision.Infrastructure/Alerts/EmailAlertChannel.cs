using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CameraVision.Infrastructure.Alerts;

/// <summary>
/// Content-agnostic e-mail sender via MailKit (System.Net.Mail.SmtpClient is not
/// recommended for new code). When the message carries an inline image path it is
/// embedded as a CID linked resource referenced by cid:inline-image@cameravision.
/// </summary>
public sealed class EmailAlertChannel(ILogger<EmailAlertChannel> logger) : IAlertChannel
{
    private const string InlineImageContentId = "inline-image@cameravision";

    public AlertChannel Channel => AlertChannel.Email;

    public async Task<bool> TrySendAsync(AlertMessage message, AlertSettings settings,
        SystemSettings system, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(system.SmtpHost) ||
            string.IsNullOrWhiteSpace(system.SmtpSenderEmail))
        {
            logger.LogWarning("Email alert skipped: SMTP host/sender not configured in system settings.");
            return false;
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(system.SmtpSenderName) ? "CameraVision" : system.SmtpSenderName,
            system.SmtpSenderEmail));
        foreach (var recipient in settings.Recipients)
        {
            if (MailboxAddress.TryParse(recipient, out var address))
                mime.To.Add(address);
        }
        if (mime.To.Count == 0)
        {
            logger.LogWarning("Email alert skipped: no valid recipient address.");
            return false;
        }

        mime.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        };
        if (message.InlineImagePath != null && File.Exists(message.InlineImagePath))
        {
            var image = await builder.LinkedResources.AddAsync(message.InlineImagePath, ct);
            image.ContentId = InlineImageContentId;
        }
        mime.Body = builder.ToMessageBody();

        var socketOptions = system.SmtpSecurity switch
        {
            SmtpSecurity.SslTls => SecureSocketOptions.SslOnConnect,
            SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.None,
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(system.SmtpHost, system.SmtpPort, socketOptions, ct);
        if (!string.IsNullOrWhiteSpace(system.SmtpUsername))
            await client.AuthenticateAsync(system.SmtpUsername, system.SmtpPassword, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(quit: true, ct);
        return true;
    }
}
