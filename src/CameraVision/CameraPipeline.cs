using System.Diagnostics;
using System.Threading.Channels;
using CameraVision.Annotation;
using CameraVision.Config;
using CameraVision.Inference;
using CameraVision.Recording;
using CameraVision.Tracking;
using CameraVision.Video;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CameraVision;

/// <summary>
/// End-to-end pipeline for one camera:
/// RTSP in (ffmpeg) → YOLO detection → IoU tracking → annotation →
/// RTSP publish to MediaMTX (ffmpeg) + rule-based MP4 recording.
/// Reconnects automatically when the camera or MediaMTX drops.
/// </summary>
public sealed class CameraPipeline(CameraInfo camera, AppConfig config, InferenceEngine engine)
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

            Log.Info(_name, "Reconnecting in 5 seconds...");
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { break; }
        }
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

        var tracker = new IouTracker(config.Recording.LostTrackTimeoutSeconds);
        var recording = new RecordingManager(_name, width, height, config.Recording,
            config.ResolvePath(config.Recording.OutputRoot));

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
        }
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
