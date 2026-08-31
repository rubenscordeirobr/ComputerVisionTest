namespace CameraVision.Core.Entities;

public enum SystemAlertType
{
    WorkerDown,
    WorkerRecovered,
}

/// <summary>
/// One system-level alert transition (SuperAdmin scope), persisted so the admin
/// can audit what happened even when notifications were suppressed by cooldown.
/// NotifiedAt is set when at least one channel delivered the notification.
/// </summary>
public class SystemAlertEvent
{
    public int Id { get; set; }

    public SystemAlertType Type { get; set; }

    /// <summary>Extra PT-BR detail shown in messages/history.</summary>
    public string? Detail { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime? NotifiedAt { get; set; }
}
