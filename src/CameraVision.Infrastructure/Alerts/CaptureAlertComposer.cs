using System.Net;
using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;

namespace CameraVision.Infrastructure.Alerts;

/// <summary>
/// PT-BR capture notifications: one message per capture, or a summary of several
/// captures for a grouped (antiflood) window. Links are tokenized playback pages.
/// </summary>
public sealed class CaptureAlertComposer(CaptureLinkService captureLinks, StoragePaths storage)
{
    private const string FallbackBaseUrl = "http://localhost:5210";

    /// <summary>
    /// The settings page wins when filled in; otherwise the deployment's
    /// CaptureLinks:PublicBaseUrl; otherwise localhost (<paramref name="usedFallback"/>).
    /// </summary>
    public string ResolveBaseUrl(SystemSettings system, out bool usedFallback)
    {
        usedFallback = false;
        var baseUrl = system.PublicBaseUrl.Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
            baseUrl = captureLinks.PublicBaseUrl;
        if (baseUrl.Length == 0)
        {
            usedFallback = true;
            baseUrl = FallbackBaseUrl;
        }
        return baseUrl;
    }

    public AlertMessage ComposeCapture(Capture capture, string baseUrl)
    {
        var labelRaw = DetectableClasses.Translate(capture.ObjectClass);
        // Tokenized link: the recipient plays this one capture without signing in.
        var playbackUrl = captureLinks.PlaybackUrl(capture.Id, baseUrl);
        var thumbnail = ResolveThumbnail(capture);

        var camera = WebUtility.HtmlEncode(capture.CameraName);
        var label = WebUtility.HtmlEncode(labelRaw);
        var started = capture.StartedAt.ToString("dd/MM/yyyy HH:mm:ss");
        var duration = capture.Duration.ToString(@"mm\:ss");

        var text =
            $"Alerta de captura — CameraVision\n\n" +
            $"Câmera: {capture.CameraName}\n" +
            $"Objeto: {labelRaw}\n" +
            $"Início: {started}\n" +
            $"Duração: {duration}\n\n" +
            $"Assista ao vídeo: {playbackUrl}\n";

        var thumbnailHtml = thumbnail == null
            ? ""
            : "<p style=\"margin:0 0 16px\"><img src=\"cid:inline-image@cameravision\" " +
              "alt=\"Miniatura da captura\" style=\"max-width:100%;border-radius:6px\" /></p>";
        var html =
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
            $"<p><a href=\"{playbackUrl}\" style=\"display:inline-block;background:#594ae2;color:#ffffff;" +
            "padding:10px 20px;border-radius:4px;text-decoration:none\">Assistir vídeo</a></p>" +
            "<p style=\"color:#888;font-size:12px\">CameraVision — alerta automático, não responda.</p>" +
            "</div>";

        return new AlertMessage(
            $"Alerta de captura — {labelRaw} em {capture.CameraName}",
            html, text, thumbnail);
    }

    public AlertMessage ComposeDigest(IReadOnlyList<Capture> items, string baseUrl)
    {
        var textLines = items.Select(c =>
            $"- {c.StartedAt:HH:mm:ss} — {DetectableClasses.Translate(c.ObjectClass)} em {c.CameraName} " +
            $"({c.Duration:mm\\:ss}): {captureLinks.PlaybackUrl(c.Id, baseUrl)}");
        var text =
            $"Resumo de capturas — CameraVision\n\n{items.Count} nova(s) captura(s):\n\n" +
            string.Join("\n", textLines) +
            "\n\nCameraVision — resumo automático, não responda.";

        var htmlItems = string.Join("", items.Select(c =>
            "<li style=\"margin:0 0 6px\">" +
            $"<b>{c.StartedAt:HH:mm:ss}</b> — {WebUtility.HtmlEncode(DetectableClasses.Translate(c.ObjectClass))} " +
            $"em <b>{WebUtility.HtmlEncode(c.CameraName)}</b> ({c.Duration:mm\\:ss}) " +
            $"<a href=\"{captureLinks.PlaybackUrl(c.Id, baseUrl)}\" style=\"color:#594ae2\">Assistir</a>" +
            "</li>"));

        var firstThumbnail = items
            .Select(ResolveThumbnail)
            .FirstOrDefault(path => path != null);
        var thumbnailHtml = firstThumbnail == null
            ? ""
            : "<p style=\"margin:0 0 16px\"><img src=\"cid:inline-image@cameravision\" " +
              "alt=\"Miniatura da primeira captura\" style=\"max-width:100%;border-radius:6px\" /></p>";

        var html =
            "<div style=\"font-family:Roboto,Arial,sans-serif;max-width:560px\">" +
            "<h2 style=\"color:#594ae2;margin:0 0 12px\">Resumo de capturas</h2>" +
            $"<p style=\"margin:0 0 16px\">{items.Count} nova(s) captura(s) no período.</p>" +
            thumbnailHtml +
            $"<ul style=\"margin:0 0 16px;padding-left:20px\">{htmlItems}</ul>" +
            "<p style=\"color:#888;font-size:12px\">CameraVision — resumo automático, não responda.</p>" +
            "</div>";

        return new AlertMessage(
            $"Resumo de capturas — {items.Count} nova(s) captura(s)",
            html, text, firstThumbnail);
    }

    private string? ResolveThumbnail(Capture capture)
    {
        if (capture.ThumbnailPath == null)
            return null;
        var path = Path.Combine(storage.OutputRoot,
            capture.ThumbnailPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? path : null;
    }
}
