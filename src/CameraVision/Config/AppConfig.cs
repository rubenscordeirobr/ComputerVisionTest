using System.Text.Json;

namespace CameraVision.Config;

public sealed class AppConfig
{
    public string CamerasFile { get; set; } = "./data/cameras.json";
    public string ModelPath { get; set; } = "./models/yolo26n.onnx";

    /// <summary>"auto" | "cuda" | "cpu"</summary>
    public string InferenceDevice { get; set; } = "auto";

    public DetectionConfig Detection { get; set; } = new();
    public MediaMtxConfig MediaMtx { get; set; } = new();
    public RecordingConfig Recording { get; set; } = new();

    /// <summary>Directory that relative paths in this config are resolved against.</summary>
    public string BaseDir { get; set; } = ".";

    public string ResolvePath(string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(BaseDir, path));

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load(string path)
    {
        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), _jsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse config file: {path}");
        config.BaseDir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        return config;
    }

    /// <summary>Searches the current directory and its parents for appsettings.json.</summary>
    public static string FindConfigFile()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "appsettings.json");
            if (File.Exists(candidate))
                return candidate;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(fallback))
            return fallback;

        throw new FileNotFoundException(
            "appsettings.json not found in the current directory, its parents, or next to the executable. " +
            "Run the app from the repository root (dotnet run --project src/CameraVision).");
    }
}

public sealed class DetectionConfig
{
    /// <summary>Minimum confidence for a detection to be considered/drawn at all.</summary>
    public float ConfidenceThreshold { get; set; } = 0.35f;

    /// <summary>Frames wider than this are downscaled before inference/annotation/publishing. 0 = keep source resolution.</summary>
    public int MaxProcessingWidth { get; set; } = 1280;
}

public sealed class MediaMtxConfig
{
    /// <summary>Annotated streams are published to {publishUrlBase}/{camera_name}.</summary>
    public string PublishUrlBase { get; set; } = "rtsp://localhost:8554/annotated";
}

public sealed class RecordingConfig
{
    /// <summary>Object class names (COCO names, e.g. "person", "car") that trigger recording.</summary>
    public string[] TrackClasses { get; set; } = ["person"];

    /// <summary>A track starts recording once its confidence reaches this value at least once.</summary>
    public float ConfidenceThreshold { get; set; } = 0.5f;

    /// <summary>Maximum duration of one recorded segment. Longer tracks produce multiple consecutive clips.</summary>
    public int MaxSegmentSeconds { get; set; } = 60;

    public string OutputRoot { get; set; } = "./output";

    /// <summary>A track is considered "left the frame" after not being matched for this long.</summary>
    public double LostTrackTimeoutSeconds { get; set; } = 2.0;
}
