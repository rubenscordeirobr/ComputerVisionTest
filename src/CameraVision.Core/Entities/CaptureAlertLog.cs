namespace CameraVision.Core.Entities;

public enum CaptureAlertStatus
{
    Success,
    Fail,
}

/// <summary>
/// One capture-alert delivery attempt per channel: written when the individual
/// alert or the grouped summary is sent (or refused, e.g. channel disabled).
/// CaptureRuleId is the rule that requested the notification.
/// </summary>
public class CaptureAlertLog
{
    public int Id { get; set; }
    public int CaptureId { get; set; }
    public int CaptureRuleId { get; set; }
    public DateTime SentAt { get; set; }
    public AlertChannel Channel { get; set; }
    public CaptureAlertStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}
