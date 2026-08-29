using System.Text.Json;

namespace CameraVision.Config;

/// <summary>
/// One entry of data/cameras.json. Only <c>name</c> and <c>rtspUrl</c> are required by the app;
/// <c>enabled</c> defaults to true when absent. <c>stream</c> ("main" | "sub", default "main")
/// selects between <c>rtspUrl</c> and the optional <c>subRtspUrl</c> — useful for cameras on a
/// weak link, where the low-bitrate substream is much more reliable.
/// Extra fields (ip, credentials, brand, ...) are ignored.
/// </summary>
public sealed class CameraInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? RtspUrl { get; set; }
    public string? SubRtspUrl { get; set; }
    public string Stream { get; set; } = "main";
    public bool? Enabled { get; set; }

    public bool IsEnabled => Enabled ?? true;

    public bool UseSubStream => Stream.Trim().Equals("sub", StringComparison.OrdinalIgnoreCase);

    /// <summary>The URL selected by <c>stream</c>; falls back to the main URL when no substream is defined.</summary>
    public string? ActiveRtspUrl => UseSubStream && !string.IsNullOrWhiteSpace(SubRtspUrl) ? SubRtspUrl : RtspUrl;

    public string ActiveStreamLabel => UseSubStream && !string.IsNullOrWhiteSpace(SubRtspUrl) ? "sub" : "main";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static List<CameraInfo> LoadAll(string path)
    {
        return JsonSerializer.Deserialize<List<CameraInfo>>(File.ReadAllText(path), _jsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse cameras file: {path}");
    }
}
