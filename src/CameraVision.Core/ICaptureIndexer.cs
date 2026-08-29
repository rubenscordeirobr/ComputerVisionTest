using CameraVision.Core.Entities;

namespace CameraVision.Core;

public sealed record IndexResult(IReadOnlyList<Capture> AddedCaptures, int RemovedCount);

/// <summary>Scans the pipeline's output folder and syncs the Capture table (idempotent).</summary>
public interface ICaptureIndexer
{
    Task<IndexResult> ScanAsync(CancellationToken ct = default);
}
