using System.Diagnostics;
using System.Threading.Channels;
using CameraVision.Annotation;
using CameraVision.ApiClient;
using CameraVision.Config;
using CameraVision.Inference;
using CameraVision.Recording;
using CameraVision.Tracking;
using CameraVision.Video;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CameraVision;

/// <summary>
/// End-to-end pipeline for one camera:
/// RTSP in (ffmpeg) → YOLO detection → IoU tracking → annotation →
/// RTSP publish to MediaMTX (ffmpeg) + rule-based MP4 recording.
/// Reconnects automatically when the camera or MediaMTX drops. When the API is
/// available it also reports runtime status and registers finished recordings
/// (with an annotated-frame thumbnail) so alerts fire immediately.
/// </summary>
public sealed class CameraPipeline(CameraInfo camera, AppConfig config, InferenceEngine engine,
    WorkerApiClient? api = null)
{
    private readonly string _name = camera.Name;

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct);
                if (!ct.IsCancellationRequested)
                    Log.Warn(_name, "Stream ended.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log.Error(_name, ex.Message);
            }

            if (ct.IsCancellationRequested)
                break;

            _ = ReportStatusAsync("reconnecting");
            Log.Info(_name, "Reconnecting in 5 seconds...");
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { break; }
        }

        await ReportStatusAsync("stopped");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var rtspUrl = camera.ActiveRtspUrl!;
        var source = await Ffmpeg.ProbeAsync(rtspUrl, ct);
        var (width, height) = ComputeProcessingSize(source);
        var fpsText = source.Fps > 0 ? $" @ {source.Fps:0.#} fps" : "";
        Log.Info(_name, $"Connected ({camera.ActiveStreamLabel} stream): " +
                        $"{source.Width}x{source.Height}{fpsText}, processing at {width}x{height}.");

        var frameSize = width * height * 3;
        var publishUrl = $"{config.MediaMtx.PublishUrlBase.TrimEnd('/')}/{_name}";

        _ = ReportStatusAsync("connected");

        var outputRoot = config.ResolvePath(config.Recording.OutputRoot);
        var tracker = new IouTracker(config.Recording.LostTrackTimeoutSeconds);
        var recording = new RecordingManager(_name, width, height, config.Recording, outputRoot,
            api == null ? null : completed => _ = RegisterCaptureAsync(completed, outputRoot));

        Process? reader = null;
        Process? publisher = null;
        try
        {
            reader = Ffmpeg.StartFrameReader(rtspUrl, width, height, _name);
            publisher = Ffmpeg.StartRtspPublisher(publishUrl, width, height, $"{_name}/pub");
            Log.Info(_name, $"Publishing annotated stream to {publishUrl}");

            // Capacity 1 + DropOldest = the processing loop always works on the freshest frame
            // and slow inference never builds up latency.
            var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });

            var readerTask = Task.Run(() => ReadFramesAsync(reader, frameSize, channel.Writer, ct), ct);
            await ProcessFramesAsync(channel.Reader, publisher, width, height, tracker, recording, ct);
            await readerTask;
        }
        finally
        {
            Ffmpeg.TryKill(reader);
            Ffmpeg.TryKill(publisher);
            await recording.FinishAllAsync(DateTime.Now);
        }
    }

    private static async Task ReadFramesAsync(Process reader, int frameSize, ChannelWriter<byte[]> writer, CancellationToken ct)
    {
        var stream = reader.StandardOutput.BaseStream;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var buffer = new byte[frameSize];
                await stream.ReadExactlyAsync(buffer, ct);
                writer.TryWrite(buffer);
            }
        }
        catch (EndOfStreamException)
        {
            // Camera closed the stream (or ffmpeg died) — pipeline restarts.
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task ProcessFramesAsync(
        ChannelReader<byte[]> frames,
        Process publisher,
        int width,
        int height,
        IouTracker tracker,
        RecordingManager recording,
        CancellationToken ct)
    {
        var statsWatch = Stopwatch.StartNew();
        var heartbeatWatch = Stopwatch.StartNew();
        var heartbeatInterval = TimeSpan.FromSeconds(Math.Max(10, config.Api.StatusIntervalSeconds));
        var processedFrames = 0;

        await foreach (var buffer in frames.ReadAllAsync(ct))
        {
            var now = DateTime.Now;

            using var image = Image.LoadPixelData<Rgb24>(buffer, width, height);
            var detections = engine.Detect(image);
            var matches = tracker.Update(detections.ToList(), now);

            Annotator.Draw(image, matches);
            image.CopyPixelDataTo(buffer);

            // Publisher gone (e.g. MediaMTX restarted) → throw so the pipeline reconnects.
            await publisher.StandardInput.BaseStream.WriteAsync(buffer, ct);

            recording.OnFrame(buffer, now, tracker.LiveTracks);

            processedFrames++;
            if (statsWatch.Elapsed.TotalSeconds >= 30)
            {
                var fps = processedFrames / statsWatch.Elapsed.TotalSeconds;
                Log.Info(_name, $"{fps:0.0} fps processed, {tracker.LiveTracks.Count} live track(s), " +
                                $"{recording.ActiveRecordings} active recording(s).");
                statsWatch.Restart();
                processedFrames = 0;
            }

            if (heartbeatWatch.Elapsed >= heartbeatInterval)
            {
                heartbeatWatch.Restart();
                _ = ReportStatusAsync("connected");
            }
        }
    }

    /// <summary>Best-effort status report — never blocks or breaks the frame loop.</summary>
    private async Task ReportStatusAsync(string status)
    {
        if (api == null || camera.Id <= 0)
            return;
        try
        {
            await api.PostStatusAsync(camera.Id, status);
        }
        catch (Exception ex)
        {
            Log.Warn(_name, $"Status report failed: {ex.Message}");
        }
    }

    /// <summary>Registers a finished recording (+ thumbnail) with the API, off the frame loop.</summary>
    private async Task RegisterCaptureAsync(CompletedRecording completed, string outputRoot)
    {
        try
        {
            var relPath = Path.GetRelativePath(outputRoot, completed.FilePath).Replace('\\', '/');
            var fileSize = new FileInfo(completed.FilePath).Length;
            var thumbnail = completed.RawFrame == null
                ? null
                : EncodeJpeg(completed.RawFrame, completed.Width, completed.Height);

            await api!.PostCaptureAsync(new ApiCaptureUpload(
                camera.Id > 0 ? camera.Id : null, _name, completed.ClassName, completed.TrackId,
                completed.StartedAt, completed.EndedAt, relPath, completed.IsMerged, fileSize), thumbnail);
            Log.Info(_name, $"Capture registered via API: {relPath}");
        }
        catch (Exception ex)
        {
            Log.Warn(_name, $"Failed to register capture via API (the indexer will pick it up): {ex.Message}");
        }
    }

    private static byte[] EncodeJpeg(byte[] rgbFrame, int width, int height)
    {
        using var image = Image.LoadPixelData<Rgb24>(rgbFrame, width, height);
        image.Mutate(x => x.Resize(320, 0));
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 80 });
        return stream.ToArray();
    }

    private (int Width, int Height) ComputeProcessingSize(StreamInfo source)
    {
        var maxWidth = config.Detection.MaxProcessingWidth;
        if (maxWidth <= 0 || source.Width <= maxWidth)
            return (MakeEven(source.Width), MakeEven(source.Height));

        var scale = maxWidth / (double)source.Width;
        return (MakeEven(maxWidth), MakeEven((int)Math.Round(source.Height * scale)));

        static int MakeEven(int value) => value - value % 2;
    }
}
