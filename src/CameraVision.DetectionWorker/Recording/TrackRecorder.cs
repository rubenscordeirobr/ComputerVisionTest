using System.Diagnostics;
using System.Globalization;
using CameraVision.Video;

namespace CameraVision.Recording;

/// <summary>A finished recording file plus the raw annotated frame kept for its thumbnail.</summary>
public sealed record CompletedRecording(
    string FilePath,
    string ClassName,
    int TrackId,
    DateTime StartedAt,
    DateTime EndedAt,
    bool IsMerged,
    byte[]? RawFrame,
    int Width,
    int Height);

/// <summary>
/// Records the annotated frames of one tracked object (one tracking ID) into
/// MP4 segments of at most maxSegmentSeconds each, and merges them into a single
/// "_full" video when the track ends. Finished files are announced through
/// onCompleted (used to register captures via the API).
/// </summary>
public sealed class TrackRecorder(
    string cameraName,
    int trackId,
    string className,
    int width,
    int height,
    string outputRoot,
    int maxSegmentSeconds,
    Action<CompletedRecording>? onCompleted = null)
{
    private readonly List<string> _segmentPaths = [];
    private readonly string _logSource = $"{cameraName}/rec#{trackId}";

    private Process? _currentProcess;
    private string _currentTempPath = "";
    private DateTime _segmentStart;
    private DateTime _trackStart;
    private bool _failed;
    private byte[]? _segmentFirstFrame;
    private byte[]? _trackFirstFrame;

    public void WriteFrame(byte[] frame, DateTime now)
    {
        if (_failed)
            return;

        if (_currentProcess == null)
        {
            _trackStart = _segmentPaths.Count == 0 ? now : _trackStart;
            StartSegment(now);
        }
        else if ((now - _segmentStart).TotalSeconds >= maxSegmentSeconds)
        {
            // Object still present when the segment closes: start the next one immediately.
            CloseSegment(now);
            StartSegment(now);
        }

        _segmentFirstFrame ??= frame;
        _trackFirstFrame ??= frame;

        try
        {
            _currentProcess!.StandardInput.BaseStream.Write(frame);
        }
        catch (Exception ex)
        {
            Log.Error(_logSource, $"Failed to write frame to recorder, stopping this recording: {ex.Message}");
            Ffmpeg.TryKill(_currentProcess);
            _currentProcess = null;
            _failed = true;
        }
    }

    /// <summary>Closes the current segment and, when there is more than one, merges them.</summary>
    public async Task FinishAsync(DateTime now)
    {
        CloseSegment(now);

        if (_segmentPaths.Count == 0)
            return;

        Log.Info(_logSource, $"Track ended: {_segmentPaths.Count} segment(s) recorded.");

        if (_segmentPaths.Count < 2)
            return;

        var directory = Path.GetDirectoryName(_segmentPaths[0])!;
        var mergedPath = UniquePath(Path.Combine(directory,
            $"{className}_{_trackStart:HH-mm-ss}_to_{now:HH-mm-ss}_full.mp4"));

        if (await Ffmpeg.ConcatAsync(_segmentPaths, mergedPath, _logSource))
        {
            Log.Info(_logSource, $"Merged track video saved: {mergedPath}");
            onCompleted?.Invoke(new CompletedRecording(
                mergedPath, className, trackId, _trackStart, now, IsMerged: true,
                _trackFirstFrame, width, height));
        }
        else
        {
            Log.Error(_logSource, "Failed to merge track segments.");
        }
    }

    private void StartSegment(DateTime now)
    {
        _segmentStart = now;
        _segmentFirstFrame = null;
        var directory = Path.Combine(outputRoot,
            now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), cameraName);
        Directory.CreateDirectory(directory);

        _currentTempPath = Path.Combine(directory, $".recording_{className}_{trackId}_{now:HHmmss}.mp4");
        _currentProcess = Ffmpeg.StartMp4Writer(_currentTempPath, width, height, _logSource);
    }

    private void CloseSegment(DateTime end)
    {
        var process = _currentProcess;
        if (process == null)
            return;
        _currentProcess = null;

        try
        {
            process.StandardInput.BaseStream.Flush();
            process.StandardInput.Close();
            if (!process.WaitForExit(10_000))
            {
                process.Kill();
                Log.Error(_logSource, "Recorder ffmpeg did not exit in time, segment discarded.");
                return;
            }

            if (process.ExitCode != 0)
            {
                Log.Error(_logSource, $"Recorder ffmpeg exited with code {process.ExitCode}, segment discarded.");
                return;
            }

            var finalPath = UniquePath(Path.Combine(Path.GetDirectoryName(_currentTempPath)!,
                $"{className}_{_segmentStart:HH-mm-ss}_to_{end:HH-mm-ss}.mp4"));
            File.Move(_currentTempPath, finalPath);
            _segmentPaths.Add(finalPath);
            Log.Info(_logSource, $"Segment saved: {finalPath}");
            onCompleted?.Invoke(new CompletedRecording(
                finalPath, className, trackId, _segmentStart, end, IsMerged: false,
                _segmentFirstFrame, width, height));
        }
        catch (Exception ex)
        {
            Log.Error(_logSource, $"Failed to close segment: {ex.Message}");
        }
        finally
        {
            try { process.Dispose(); } catch { }
        }
    }

    /// <summary>Appends the track id when two tracks would produce the same file name.</summary>
    private string UniquePath(string path)
    {
        if (!File.Exists(path))
            return path;
        return Path.Combine(Path.GetDirectoryName(path)!,
            $"{Path.GetFileNameWithoutExtension(path)}_track{trackId}.mp4");
    }
}
