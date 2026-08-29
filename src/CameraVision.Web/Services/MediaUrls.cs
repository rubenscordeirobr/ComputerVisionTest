namespace CameraVision.Web.Services;

/// <summary>
/// Builds absolute media URLs pointing at the API's file streaming service
/// (config Api:MediaBaseUrl) — the web app no longer serves /media itself.
/// </summary>
public sealed class MediaUrls(string baseUrl)
{
    public string For(string relativePath) => $"{baseUrl}/media/{relativePath}";
}
