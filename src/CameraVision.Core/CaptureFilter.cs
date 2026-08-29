using CameraVision.Core.Entities;

namespace CameraVision.Core;

/// <summary>Filter + paging for capture queries. Date bounds compare against StartedAt.</summary>
public sealed record CaptureFilter
{
    public DateTime? DateFrom { get; init; }

    /// <summary>Exclusive upper bound.</summary>
    public DateTime? DateTo { get; init; }

    public string? CameraName { get; init; }
    public string? ObjectClass { get; init; }
    public int? TrackId { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 25;
}

public sealed record CapturePage(IReadOnlyList<Capture> Items, int TotalCount);
