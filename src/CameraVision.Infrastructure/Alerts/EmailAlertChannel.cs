using System.Net;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Utils;

namespace CameraVision.Infrastructure.Alerts;

/// <summary>
/// Sends the capture alert e-mail via MailKit (System.Net.Mail.SmtpClient is not
/// recommended for new code): PT-BR HTML body with the thumbnail embedded inline
/// (CID) and a link to the in-app playback page — never the video file itself.
/// </summary>
public sealed class EmailAlertChannel(ILogger<EmailAlertChannel> logger) : IAlertChannel
{
    public AlertChannel Channel => AlertChannel.Email;

    public async Task<bool> TrySendAsync(CaptureAlert alert, AlertSettings rules,
        SystemSettings system, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(system.SmtpHost) ||
            string.IsNullOrWhiteSpace(system.SmtpSenderEmail))
        {
            logger.LogWarning("Email alert skipped: SMTP host/sender not configured in system settings.");
            return false;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(system.SmtpSenderName) ? "CameraVision" : system.SmtpSenderName,
            system.SmtpSenderEmail));
        foreach (var recipient in rules.Recipients)
        {
            if (MailboxAddress.TryParse(recipient, out var address))
                message.To.Add(address);
        }
        if (message.To.Count == 0)
        {
            logger.LogWarning("Email alert skipped: no valid recipient address.");
            return false;
        }

        var capture = alert.Capture;
        message.Subject = $"Alerta de captura — {alert.ClassLabelPtBr} em {capture.CameraName}";

        var builder = new BodyBuilder();
        string? contentId = null;
        if (alert.ThumbnailAbsolutePath != null)
        {
            var image = await builder.LinkedResources.AddAsync(alert.ThumbnailAbsolutePath, ct);
            image.ContentId = MimeUtils.GenerateMessageId();
            contentId = image.ContentId;
        }

        var camera = WebUtility.HtmlEncode(capture.CameraName);
        var label = WebUtility.HtmlEncode(alert.ClassLabelPtBr);
        var started = capture.StartedAt.ToString("dd/MM/yyyy HH:mm:ss");
        var duration = capture.Duration.ToString(@"mm\:ss");

        builder.TextBody =
            $"Alerta de captura — CameraVision\n\n" +
            $"Câmera: {capture.CameraName}\n" +
            $"Objeto: {alert.ClassLabelPtBr}\n" +
            $"Início: {started}\n" +
            $"Duração: {duration}\n\n" +
            $"Assista ao vídeo: {alert.PlaybackUrl}\n";

        var thumbnailHtml = contentId == null
            ? ""
            : $"<p style=\"margin:0 0 16px\"><img src=\"cid:{contentId}\" alt=\"Miniatura da captura\" " +
              "style=\"max-width:100%;border-radius:6px\" /></p>";
        builder.HtmlBody =
            "<div style=\"font-family:Roboto,Arial,sans-serif;max-width:520px\">" +
            "<h2 style=\"color:#594ae2;margin:0 0 12px\">Alerta de captura</h2>" +
            $"<p style=\"margin:0 0 16px\">Um objeto <b>{label}</b> foi detectado na câmera <b>{camera}</b>.</p>" +
            thumbnailHtml +
            "<table style=\"border-collapse:collapse;margin:0 0 16px\">" +
            $"<tr><td style=\"padding:2px 12px 2px 0;color:#666\">Câmera</td><td><b>{camera}</b></td></tr>" +
            $"<tr><td style=\"padding:2px 12px 2px 0;color:#666\">Objeto</td><td><b>{label}</b></td></tr>" +
            $"<tr><td style=\"padding:2px 12px 2px 0;color:#666\">Início</td><td>{started}</td></tr>" +
            $"<tr><td style=\"padding:2px 12px 2px 0;color:#666\">Duração</td><td>{duration}</td></tr>" +
            "</table>" +
            $"<p><a href=\"{alert.PlaybackUrl}\" style=\"display:inline-block;background:#594ae2;color:#ffffff;" +
            "padding:10px 20px;border-radius:4px;text-decoration:none\">Assistir vídeo</a></p>" +
            "<p style=\"color:#888;font-size:12px\">CameraVision — alerta automático, não responda.</p>" +
            "</div>";

        message.Body = builder.ToMessageBody();

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
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
        return true;
    }
}
