namespace CameraVision.Core.Entities;

public enum HealthCondition
{
    Offline,
    Weak,
    Recovered,
}

/// <summary>
/// One camera health state transition. Every event is persisted so digests can be
/// built and the user can audit what happened even while notifications were
/// suppressed. NotifiedAt = individually notified; DigestedAt = included in a
/// digest (or intentionally consumed without notification); Suppressed = held by
/// cooldown/flood cap and waiting for the next digest.
/// </summary>
public class CameraHealthEvent
{
    public int Id { get; set; }

    /// <summary>Copied from the camera so digests route to the right tenant after deletion.</summary>
    public int TenantId { get; set; }

    public int? CameraId { get; set; }
    public string CameraName { get; set; } = "";
    public HealthCondition Condition { get; set; }

    /// <summary>Extra PT-BR detail shown in messages/history (e.g. "latência 820 ms").</summary>
    public string? Detail { get; set; }

    public DateTime OccurredAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public bool Suppressed { get; set; }
    public DateTime? DigestedAt { get; set; }
}
