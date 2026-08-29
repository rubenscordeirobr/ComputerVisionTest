using Compunet.YoloSharp.Data;
using SixLabors.ImageSharp;

namespace CameraVision.Tracking;

public sealed class TrackedObject
{
    public required int Id { get; init; }
    public required int ClassId { get; init; }
    public required string ClassName { get; init; }
    public float Confidence { get; set; }
    public float MaxConfidence { get; set; }
    public Rectangle Bounds { get; set; }
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// Minimal IoU-based tracker: detections are greedily matched to existing tracks of the same
/// class by highest IoU. Unmatched detections become new tracks; tracks unseen for longer than
/// the lost-track timeout are dropped ("object left the frame").
/// </summary>
public sealed class IouTracker(double lostTrackTimeoutSeconds)
{
    private const float MinIou = 0.2f;

    private readonly List<TrackedObject> _tracks = [];
    private int _nextId = 1;

    /// <summary>All tracks currently considered alive (including briefly occluded ones).</summary>
    public IReadOnlyList<TrackedObject> LiveTracks => _tracks;

    /// <summary>Tracks removed during the last Update call.</summary>
    public List<TrackedObject> RemovedTracks { get; } = [];

    public List<(Detection Detection, TrackedObject Track)> Update(IReadOnlyList<Detection> detections, DateTime now)
    {
        // Collect all (track, detection) candidate pairs above the IoU threshold.
        var candidates = new List<(float Iou, int DetectionIndex, TrackedObject Track)>();
        for (var i = 0; i < detections.Count; i++)
        {
            foreach (var track in _tracks)
            {
                if (track.ClassId != detections[i].Name.Id)
                    continue;
                var iou = ComputeIou(track.Bounds, detections[i].Bounds);
                if (iou >= MinIou)
                    candidates.Add((iou, i, track));
            }
        }

        var matches = new List<(Detection, TrackedObject)>();
        var matchedDetections = new bool[detections.Count];
        var matchedTracks = new HashSet<int>();

        foreach (var (_, detectionIndex, track) in candidates.OrderByDescending(c => c.Iou))
        {
            if (matchedDetections[detectionIndex] || !matchedTracks.Add(track.Id))
                continue;
            matchedDetections[detectionIndex] = true;

            var detection = detections[detectionIndex];
            track.Bounds = detection.Bounds;
            track.Confidence = detection.Confidence;
            track.MaxConfidence = Math.Max(track.MaxConfidence, detection.Confidence);
            track.LastSeen = now;
            matches.Add((detection, track));
        }

        for (var i = 0; i < detections.Count; i++)
        {
            if (matchedDetections[i])
                continue;
            var detection = detections[i];
            var track = new TrackedObject
            {
                Id = _nextId++,
                ClassId = detection.Name.Id,
                ClassName = detection.Name.Name,
                Confidence = detection.Confidence,
                MaxConfidence = detection.Confidence,
                Bounds = detection.Bounds,
                FirstSeen = now,
                LastSeen = now,
            };
            _tracks.Add(track);
            matches.Add((detection, track));
        }

        RemovedTracks.Clear();
        _tracks.RemoveAll(track =>
        {
            if ((now - track.LastSeen).TotalSeconds <= lostTrackTimeoutSeconds)
                return false;
            RemovedTracks.Add(track);
            return true;
        });

        return matches;
    }

    private static float ComputeIou(Rectangle a, Rectangle b)
    {
        var intersection = Rectangle.Intersect(a, b);
        if (intersection.IsEmpty)
            return 0;
        float intersectionArea = intersection.Width * (float)intersection.Height;
        var unionArea = a.Width * (float)a.Height + b.Width * (float)b.Height - intersectionArea;
        return unionArea <= 0 ? 0 : intersectionArea / unionArea;
    }
}
