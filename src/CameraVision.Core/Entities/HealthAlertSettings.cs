namespace CameraVision.Core.Entities;

/// <summary>
/// Singleton row (Id = 1): camera health alerting + anti-flood configuration.
/// Precedence when an event is recorded: cooldown → flood cap → digest.
/// </summary>
public class HealthAlertSettings
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; }
    public bool NotifyEmail { get; set; } = true;
    public bool NotifyWhatsApp { get; set; }

    /// <summary>Latency above this marks a camera as Weak.</summary>
    public int WeakLatencyMs { get; set; } = 500;

    /// <summary>A condition must persist this many consecutive checks before alerting.</summary>
    public int ConsecutiveChecks { get; set; } = 3;

    public bool NotifyRecovery { get; set; } = true;

    /// <summary>Minimum minutes between notifications for the same camera + condition.</summary>
    public int CooldownMinutes { get; set; } = 10;

    /// <summary>Global cap: at most FloodCapCount notifications per FloodCapWindowMinutes.</summary>
    public int FloodCapCount { get; set; } = 10;

    public int FloodCapWindowMinutes { get; set; } = 60;

    /// <summary>When on, individual messages are replaced by one grouped digest per interval.</summary>
    public bool DigestEnabled { get; set; }

    public int DigestIntervalMinutes { get; set; } = 15;

    public DateTime? LastDigestAt { get; set; }
}
