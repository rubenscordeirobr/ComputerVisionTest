using CameraVision.Config;
using CameraVision.Tracking;

namespace CameraVision.Recording;

/// <summary>
/// Per-camera recording rules: while a track of a configured class (that reached the
/// recording confidence threshold at least once) is alive, its annotated frames are written
/// through a TrackRecorder. When the track dies the recorder is finished and merged.
/// </summary>
public sealed class RecordingManager(
    string cameraName, int width, int height, RecordingConfig config, string outputRoot,
    Action<CompletedRecording>? onCompleted = null)
{
    private readonly Dictionary<int, TrackRecorder> _recorders = [];

    public int ActiveRecordings => _recorders.Count;

    public void OnFrame(byte[] annotatedFrame, DateTime now, IReadOnlyList<TrackedObject> liveTracks)
    {
        HashSet<int>? liveRecordableIds = null;

        foreach (var track in liveTracks)
        {
            // Null threshold = class untracked or all matching rules outside their time window.
            var threshold = config.ActiveThresholdFor(track.ClassName, now);
            if (threshold == null || track.MaxConfidence < threshold.Value)
                continue;

            (liveRecordableIds ??= []).Add(track.Id);

            if (!_recorders.TryGetValue(track.Id, out var recorder))
            {
                recorder = new TrackRecorder(cameraName, track.Id, track.ClassName,
                    width, height, outputRoot, config.MaxSegmentSeconds, onCompleted);
                _recorders[track.Id] = recorder;
                Log.Info(cameraName, $"Recording started: {track.ClassName} #{track.Id} " +
                                     $"(confidence {track.Confidence:0.00})");
            }

            recorder.WriteFrame(annotatedFrame, now);
        }

        if (_recorders.Count == 0)
            return;

        foreach (var trackId in _recorders.Keys.ToList())
        {
            if (liveRecordableIds != null && liveRecordableIds.Contains(trackId))
                continue;
            var recorder = _recorders[trackId];
            _recorders.Remove(trackId);
            _ = Task.Run(() => FinishSafelyAsync(recorder, now)); // don't stall the frame loop on ffmpeg exit
        }
    }

    public async Task FinishAllAsync(DateTime now)
    {
        var recorders = _recorders.Values.ToList();
        _recorders.Clear();
        foreach (var recorder in recorders)
            await FinishSafelyAsync(recorder, now);
    }

    private async Task FinishSafelyAsync(TrackRecorder recorder, DateTime now)
    {
        try
        {
            await recorder.FinishAsync(now);
        }
        catch (Exception ex)
        {
            Log.Error(cameraName, $"Failed to finish recording: {ex.Message}");
        }
    }
}
