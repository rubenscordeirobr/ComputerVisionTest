using CameraVision;
using CameraVision.Config;
using CameraVision.Inference;
using CameraVision.Video;

Console.OutputEncoding = System.Text.Encoding.UTF8;

AppConfig config;
List<CameraInfo> cameras;
try
{
    var configPath = AppConfig.FindConfigFile();
    config = AppConfig.Load(configPath);
    Log.Info("startup", $"Config: {configPath}");

    Ffmpeg.EnsureAvailable();

    cameras = CameraInfo.LoadAll(config.ResolvePath(config.CamerasFile));
}
catch (Exception ex)
{
    Log.Error("startup", ex.Message);
    return 1;
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
        Log.Warn("startup", $"Camera '{camera.Name}' has stream=sub but no 'subRtspUrl'; using the main stream.");
    enabledCameras.Add(camera);
}

if (enabledCameras.Count == 0)
{
    Log.Error("startup", "No enabled cameras found in cameras.json.");
    return 1;
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
                    $"segments of {config.Recording.MaxSegmentSeconds}s)");
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

var pipelines = enabledCameras
    .Select(camera => new CameraPipeline(camera, config, engine).RunAsync(cts.Token))
    .ToArray();

Log.Info("startup", $"Started {pipelines.Length} camera pipeline(s). Press Ctrl+C to stop.");
await Task.WhenAll(pipelines);

engine.Dispose();
Log.Info("shutdown", "Done.");
return 0;
