namespace CameraVision.Api;

public sealed record ProcessorCameraDto(
    int Id, string Name, string StreamUrl, string? SubStreamUrl, string PreferredStream);

public sealed record WorkerRuleDto(
    IReadOnlyList<string> Classes,
    double ConfidenceThreshold,
    TimeOnly? ActiveFrom,
    TimeOnly? ActiveTo);

/// <summary>Enabled capture rules for the worker (windows evaluated live on its side)
/// plus the longest segment/linger across rules.</summary>
public sealed record CaptureRulesDto(
    IReadOnlyList<WorkerRuleDto> Rules, int MaxSegmentSeconds, double LingerSeconds);

public sealed record CameraStatusDto(string Status, string? Detail);

public sealed record WorkerHeartbeatDto(DateTime? StartedAt, string? Device, int ActiveCameras);

public sealed record CaptureIngestDto(
    int? CameraId,
    string CameraName,
    string ObjectClass,
    int? TrackId,
    DateTime StartedAt,
    DateTime EndedAt,
    string FilePath,
    bool IsMerged,
    long FileSizeBytes);
