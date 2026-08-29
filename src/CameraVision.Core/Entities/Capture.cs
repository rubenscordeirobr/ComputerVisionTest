namespace CameraVision.Core.Entities;

/// <summary>One recorded MP4 file imported from the pipeline's output folder.</summary>
public class Capture
{
    public int Id { get; set; }

    /// <summary>Camera matched (or auto-created) by name; null only if the camera was deleted later.</summary>
    public int? CameraId { get; set; }

    /// <summary>Camera folder name as recorded on disk.</summary>
    public string CameraName { get; set; } = "";

    /// <summary>English COCO class name (e.g. "person").</summary>
    public string ObjectClass { get; set; } = "";

    /// <summary>Only known when the file name carries a "_track{id}" collision suffix.</summary>
    public int? TrackId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }

    /// <summary>Path relative to the output root, with '/' separators. Unique.</summary>
    public string FilePath { get; set; } = "";

    public string? ThumbnailPath { get; set; }

    /// <summary>True for "_full" files (merged multi-segment tracks).</summary>
    public bool IsMerged { get; set; }

    public long FileSizeBytes { get; set; }
    public DateTime IndexedAt { get; set; }

    public TimeSpan Duration => EndedAt - StartedAt;
}
