using System.Diagnostics;
using System.Globalization;

namespace CameraVision.Video;

public sealed record StreamInfo(int Width, int Height, double Fps);

public static class Ffmpeg
{
    /// <summary>Throws with a clear message when ffmpeg/ffprobe are not on PATH.</summary>
    public static void EnsureAvailable()
    {
        foreach (var tool in new[] { "ffmpeg", "ffprobe" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tool,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                process!.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"'{tool}' was not found on PATH. Install FFmpeg (winget install Gyan.FFmpeg) and restart.", ex);
            }
        }
    }

    /// <summary>Probes an RTSP stream for resolution and frame rate.</summary>
    public static async Task<StreamInfo> ProbeAsync(string rtspUrl, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = "-v error -rtsp_transport tcp " +
                        "-select_streams v:0 -show_entries stream=width,height,avg_frame_rate " +
                        $"-of csv=p=0 \"{rtspUrl}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start ffprobe.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));

        var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            throw new TimeoutException($"ffprobe timed out for '{rtspUrl}' — camera unreachable?");
        }

        var output = (await outputTask).Trim();
        if (process.ExitCode != 0 || output.Length == 0)
        {
            var error = (await errorTask).Trim();
            throw new InvalidOperationException($"ffprobe failed for '{rtspUrl}': {error}");
        }

        // Example output: 1920,1080,20/1
        var parts = output.Split(',');
        var width = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var height = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var fps = 0.0;
        if (parts.Length > 2 && parts[2].Contains('/'))
        {
            var fraction = parts[2].Split('/');
            var numerator = double.Parse(fraction[0], CultureInfo.InvariantCulture);
            var denominator = double.Parse(fraction[1], CultureInfo.InvariantCulture);
            if (denominator > 0)
                fps = numerator / denominator;
        }

        return new StreamInfo(width, height, fps);
    }

    /// <summary>
    /// Starts an ffmpeg process that decodes the RTSP stream to raw RGB24 frames on stdout,
    /// scaled to width x height.
    /// </summary>
    public static Process StartFrameReader(string rtspUrl, int width, int height, string logSource)
    {
        return Start(
            "-hide_banner -loglevel warning -nostats " +
            "-rtsp_transport tcp " +
            $"-i \"{rtspUrl}\" " +
            "-an -sn " +
            $"-vf scale={width}:{height} " +
            "-f rawvideo -pix_fmt rgb24 pipe:1",
            logSource, redirectStdin: false, redirectStdout: true);
    }

    /// <summary>
    /// Starts an ffmpeg process that reads raw RGB24 frames from stdin (timestamped with the
    /// wall clock, so any processing rate works) and publishes H.264 over RTSP to MediaMTX.
    /// </summary>
    public static Process StartRtspPublisher(string publishUrl, int width, int height, string logSource)
    {
        // loglevel error: wall-clock timestamps occasionally produce duplicate DTS values,
        // which ffmpeg fixes itself but reports with a recurring warning.
        return Start(
            "-hide_banner -loglevel error -nostats " +
            $"-f rawvideo -pixel_format rgb24 -video_size {width}x{height} " +
            "-use_wallclock_as_timestamps 1 -i pipe:0 " +
            "-c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p " +
            // Keyframe every 2 seconds of wall time (frame-count GOPs would stretch for
            // many seconds at low processing fps, delaying WebRTC/HLS playback start).
            "-force_key_frames expr:gte(t,n_forced*2) -bf 0 -b:v 2500k " +
            "-fps_mode passthrough " +
            $"-f rtsp -rtsp_transport tcp \"{publishUrl}\"",
            logSource, redirectStdin: true, redirectStdout: false);
    }

    /// <summary>
    /// Starts an ffmpeg process that reads raw RGB24 frames from stdin and writes an MP4 segment.
    /// </summary>
    public static Process StartMp4Writer(string outputPath, int width, int height, string logSource)
    {
        return Start(
            "-hide_banner -loglevel error -nostats " +
            $"-f rawvideo -pixel_format rgb24 -video_size {width}x{height} " +
            "-use_wallclock_as_timestamps 1 -i pipe:0 " +
            "-c:v libx264 -preset ultrafast -crf 23 -pix_fmt yuv420p " +
            "-fps_mode passthrough -movflags +faststart " +
            $"-y \"{outputPath}\"",
            logSource, redirectStdin: true, redirectStdout: false);
    }

    /// <summary>Concatenates MP4 segments (same encoding parameters) without re-encoding.</summary>
    public static async Task<bool> ConcatAsync(IReadOnlyList<string> segmentPaths, string outputPath, string logSource)
    {
        var listPath = Path.Combine(Path.GetTempPath(), $"concat_{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(listPath,
            segmentPaths.Select(p => $"file '{Path.GetFullPath(p).Replace('\\', '/')}'"));

        try
        {
            using var process = Start(
                "-hide_banner -loglevel error -nostats " +
                $"-f concat -safe 0 -i \"{listPath}\" -c copy -y \"{outputPath}\"",
                logSource, redirectStdin: false, redirectStdout: false);
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        finally
        {
            try { File.Delete(listPath); } catch { }
        }
    }

    private static Process Start(string arguments, string logSource, bool redirectStdin, bool redirectStdout)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                RedirectStandardInput = redirectStdin,
                RedirectStandardOutput = redirectStdout,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Log.Ffmpeg(logSource, e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException("Failed to start ffmpeg.");
        process.BeginErrorReadLine();
        return process;
    }

    public static void TryKill(Process? process)
    {
        if (process == null)
            return;
        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch { }
        try { process.Dispose(); } catch { }
    }
}
