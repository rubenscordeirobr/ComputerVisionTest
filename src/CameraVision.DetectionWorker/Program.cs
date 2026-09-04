using CameraVision;
using CameraVision.Annotation;
using CameraVision.ApiClient;
using CameraVision.Config;
using CameraVision.Inference;
using CameraVision.Video;

Console.OutputEncoding = System.Text.Encoding.UTF8;

AppConfig config;
try
{
    var configPath = AppConfig.FindConfigFile();
    config = AppConfig.Load(configPath);
    Log.Info("startup", $"Config: {configPath}");

    Ffmpeg.EnsureAvailable();
}
catch (Exception ex)
{
    Log.Error("startup", ex.Message);
    return 1;
}

// Cameras and capture rules come from the API; cameras.json and the local
// recording settings are the fallback so surveillance keeps working when the
// API is down. Changes made in the web app require a worker restart.
List<CameraInfo> cameras;
WorkerApiClient? api = null;
try
{
    api = new WorkerApiClient(config.Api);
    var apiCameras = await api.GetCamerasAsync();
    var rules = await api.GetCaptureRulesAsync();

    cameras = apiCameras.Select(c => new CameraInfo
    {
        Id = c.Id,
        Name = c.Name,
        RtspUrl = c.StreamUrl,
        SubRtspUrl = c.SubStreamUrl,
        Stream = c.PreferredStream,
        Enabled = true,
    }).ToList();

    config.Recording.Rules = rules.Rules.Select(r => new RecordingRule
    {
        Classes = new HashSet<string>(r.Classes, StringComparer.OrdinalIgnoreCase),
        ClassColors = new Dictionary<string, string>(
            r.ClassColors ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
        ConfidenceThreshold = (float)r.ConfidenceThreshold,
        ActiveFrom = r.ActiveFrom,
        ActiveTo = r.ActiveTo,
    }).ToList();
    config.Recording.TrackClasses = rules.Rules
        .SelectMany(r => r.Classes)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    // Annotation colors: the first enabled rule that sets a color for a class wins.
    config.Recording.ClassColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var rule in config.Recording.Rules)
        foreach (var (className, hex) in rule.ClassColors)
            config.Recording.ClassColors.TryAdd(className, hex);
    if (rules.Rules.Count > 0)
        config.Recording.ConfidenceThreshold = (float)rules.Rules.Min(r => r.ConfidenceThreshold);
    else
        Log.Warn("startup", "API returned no enabled capture rules — nothing records until a rule is created.");
    config.Recording.MaxSegmentSeconds = rules.MaxSegmentSeconds;
    config.Recording.LostTrackTimeoutSeconds = rules.LingerSeconds;

    var windowed = rules.Rules.Count(r => r.ActiveFrom != null && r.ActiveTo != null);
    Log.Info("startup", $"Loaded {cameras.Count} camera(s) and {rules.Rules.Count} capture rule(s) " +
                        $"({windowed} time-windowed) from API {config.Api.BaseUrl}.");
}
catch (Exception ex)
{
    api?.Dispose();
    api = null;
    Log.Warn("startup", $"API unavailable ({ex.Message}) — " +
                        "falling back to cameras.json + local recording settings.");
    try
    {
        cameras = CameraInfo.LoadAll(config.ResolvePath(config.CamerasFile));
    }
    catch (Exception fallbackEx)
    {
        Log.Error("startup", fallbackEx.Message);
        return 1;
    }
}

var enabledCameras = new List<CameraInfo>();
foreach (var camera in cameras)
{
    if (string.IsNullOrWhiteSpace(camera.Name) || string.IsNullOrWhiteSpace(camera.RtspUrl))
    {
        Log.Warn("startup", $"Skipping camera entry id={camera.Id}: 'name' and 'rtspUrl' are required.");
        continue;
    }
    if (!camera.IsEnabled)
    {
        Log.Info("startup", $"Camera '{camera.Name}' is disabled, skipping.");
        continue;
    }
    if (camera.UseSubStream && string.IsNullOrWhiteSpace(camera.SubRtspUrl))
        Log.Warn("startup", $"Camera '{camera.Name}' has stream=sub but no substream URL; using the main stream.");
    enabledCameras.Add(camera);
}

if (enabledCameras.Count == 0)
{
    Log.Error("startup", "No enabled cameras available (API and/or cameras.json).");
    return 1;
}

// The portable CUDA runtime (repo-local ./cuda-runtime, gitignored) must be on PATH
// for the ONNX Runtime CUDA provider. Self-register it so GPU inference works no
// matter how the worker is launched (services/schedulers don't get the user PATH).
var cudaRuntimeDir = config.ResolvePath("./cuda-runtime");
if (Directory.Exists(cudaRuntimeDir))
{
    Environment.SetEnvironmentVariable("PATH",
        cudaRuntimeDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
    Log.Info("startup", $"Portable CUDA runtime added to PATH: {cudaRuntimeDir}");
}

InferenceEngine engine;
try
{
    Log.Info("startup", $"Loading model: {config.ResolvePath(config.ModelPath)} (device setting: {config.InferenceDevice})");
    engine = InferenceEngine.Create(config);
}
catch (Exception ex)
{
    Log.Error("startup", ex.Message);
    return 1;
}

Log.Info("startup", $"Inference device: {engine.DeviceDescription}");

var unknownClasses = config.Recording.TrackClasses
    .Where(c => !engine.ClassNames.Contains(c, StringComparer.OrdinalIgnoreCase))
    .ToList();
if (unknownClasses.Count > 0)
    Log.Warn("startup", $"trackClasses not present in the model: {string.Join(", ", unknownClasses)}");

Log.Info("startup", $"Tracking classes: {string.Join(", ", config.Recording.TrackClasses)} " +
                    $"(record >= {config.Recording.ConfidenceThreshold:0.00}, " +
                    $"segments of {config.Recording.MaxSegmentSeconds}s); only these are annotated.");
var annotator = new Annotator(config.Recording.ClassColors);
if (annotator.CustomColors.Count > 0)
    Log.Info("startup", "Annotation colors: " +
                        string.Join(", ", annotator.CustomColors.Select(kv => $"{kv.Key}={kv.Value}")));
Log.Info("startup", $"Publishing to: {config.MediaMtx.PublishUrlBase}/<camera_name>");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (!cts.IsCancellationRequested)
    {
        Log.Info("shutdown", "Stopping (Ctrl+C)...");
        cts.Cancel();
    }
};

// Global heartbeat so the web app can tell "worker running" apart from "worker
// stopped" even while every camera is reconnecting. Same cadence as the
// per-camera status reports.
var heartbeat = Task.CompletedTask;
if (api != null)
{
    var startedAt = DateTime.Now;
    var heartbeatApi = api;
    heartbeat = Task.Run(async () =>
    {
        var interval = TimeSpan.FromSeconds(Math.Max(10, config.Api.StatusIntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try
                {
                    await heartbeatApi.PostHeartbeatAsync(startedAt, engine.DeviceDescription,
                        enabledCameras.Count, cts.Token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.Warn("heartbeat", $"Heartbeat failed: {ex.Message}");
                }
            }
            while (await timer.WaitForNextTickAsync(cts.Token));
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    });
}

var pipelines = enabledCameras
    .Select(camera => new CameraPipeline(camera, config, engine, annotator, api).RunAsync(cts.Token))
    .ToArray();

Log.Info("startup", $"Started {pipelines.Length} camera pipeline(s). Press Ctrl+C to stop.");
await Task.WhenAll(pipelines);
await heartbeat;

engine.Dispose();
api?.Dispose();
Log.Info("shutdown", "Done.");
return 0;
