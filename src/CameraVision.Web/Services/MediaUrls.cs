using CameraVision.Core.Alerts;

namespace CameraVision.Web.Services;

/// <summary>
/// Builds absolute media URLs pointing at the API's file streaming service
/// (config Api:MediaBaseUrl) — the web app no longer serves /media itself.
/// Every URL carries the capture's playback token: the API is a different
/// origin whenever the web app is reached on another host or port, and the
/// browser leaves the shared auth cookie out of such cross-site image, video
/// and download requests — the token authorizes that single file instead.
/// </summary>
public sealed class MediaUrls(string baseUrl, CaptureLinkService links)
{
    public string For(int captureId, string relativePath) =>
        links.MediaUrl(captureId, relativePath, baseUrl);
}
