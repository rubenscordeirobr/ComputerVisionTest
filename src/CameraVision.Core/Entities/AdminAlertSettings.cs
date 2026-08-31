namespace CameraVision.Core.Entities;

/// <summary>
/// Singleton row (Id = 1): critical system alerts sent to the system
/// administrators (SuperAdmin scope) — currently DetectionWorker down/recovered.
/// Independent from the per-tenant capture/health alerts: the recipients live
/// here, not in AlertSettings.
/// </summary>
public class AdminAlertSettings
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool NotifyEmail { get; set; } = true;

    public bool NotifyWhatsApp { get; set; } = true;

    /// <summary>System administrator e-mail addresses.</summary>
    public List<string> Emails { get; set; } = [];

    /// <summary>System administrator WhatsApp numbers (e.g. +5549999999999).</summary>
    public List<string> WhatsAppNumbers { get; set; } = [];

    /// <summary>The worker counts as down after this long without any status update.</summary>
    public int WorkerDownAfterSeconds { get; set; } = 90;

    /// <summary>Minimum minutes between notifications of the same alert type.</summary>
    public int CooldownMinutes { get; set; } = 30;

    public bool NotifyRecovery { get; set; } = true;
}
