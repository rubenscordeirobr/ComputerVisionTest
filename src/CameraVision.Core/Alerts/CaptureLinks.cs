using System.Security.Cryptography;
using System.Text;

namespace CameraVision.Core.Alerts;

/// <summary>
/// Host configuration for the public capture links sent in alerts
/// (config section <c>CaptureLinks</c>).
/// </summary>
public sealed class CaptureLinkOptions
{
    /// <summary>Public base URL of the web app, e.g. http://45.238.108.200:5210.</summary>
    public string PublicBaseUrl { get; init; } = "";

    /// <summary>Public base URL of the API that streams the media files.</summary>
    public string MediaBaseUrl { get; init; } = "";

    /// <summary>HMAC key for the playback tokens. Must match across Web and Api.</summary>
    public string Secret { get; init; } = "";
}

/// <summary>
/// Builds and validates the unguessable playback links used in alert e-mails.
/// The token is an HMAC over the capture id, so a recipient can open the
/// playback page and stream that single capture without signing in — and
/// cannot reach any other capture by editing the id.
/// </summary>
public sealed class CaptureLinkService
{
    public const string TokenQueryKey = "token";

    private const int TokenBytes = 18; // 144 bits → 24 base64url chars
    private const string FallbackSecret = "cameravision-dev-capture-link-secret";

    private readonly CaptureLinkOptions options;
    private readonly byte[] key;

    public CaptureLinkService(CaptureLinkOptions options)
    {
        this.options = options;
        key = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(options.Secret)
            ? FallbackSecret
            : options.Secret);
    }

    /// <summary>Configured public base URL of the web app, without a trailing slash.</summary>
    public string PublicBaseUrl => Normalize(options.PublicBaseUrl);

    /// <summary>Configured public base URL of the media API, without a trailing slash.</summary>
    public string MediaBaseUrl => Normalize(options.MediaBaseUrl);

    public string CreateToken(int captureId)
    {
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"capture:{captureId}"));
        return Base64Url(hash.AsSpan(0, TokenBytes));
    }

    public bool IsValidToken(int captureId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(CreateToken(captureId)),
            Encoding.UTF8.GetBytes(token));
    }

    /// <summary>Tokenized public playback page link, e.g. {base}/captures/12/watch?token=…</summary>
    public string PlaybackUrl(int captureId, string? baseUrlOverride = null)
    {
        var baseUrl = Normalize(baseUrlOverride);
        if (baseUrl.Length == 0)
            baseUrl = PublicBaseUrl;
        return $"{baseUrl}/captures/{captureId}/watch?{TokenQueryKey}={CreateToken(captureId)}";
    }

    /// <summary>Tokenized media link accepted by the API without an auth cookie.</summary>
    public string MediaUrl(int captureId, string relativeFilePath, string? baseUrlOverride = null)
    {
        var baseUrl = Normalize(baseUrlOverride);
        if (baseUrl.Length == 0)
            baseUrl = MediaBaseUrl;
        return $"{baseUrl}/media/{relativeFilePath}?{TokenQueryKey}={CreateToken(captureId)}";
    }

    private static string Normalize(string? url) =>
        string.IsNullOrWhiteSpace(url) ? "" : url.Trim().TrimEnd('/');

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
