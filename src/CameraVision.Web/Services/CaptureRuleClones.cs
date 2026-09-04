using CameraVision.Core.Entities;

namespace CameraVision.Web.Services;

/// <summary>Deep copies for the edit dialogs, so cancelling leaves the displayed rows untouched.</summary>
public static class CaptureRuleClones
{
    public static AlertTrigger Clone(AlertTrigger trigger) => new()
    {
        Id = trigger.Id,
        CaptureRuleId = trigger.CaptureRuleId,
        Enabled = trigger.Enabled,
        Channel = trigger.Channel,
        ContactIds = [.. trigger.ContactIds],
        Kind = trigger.Kind,
        Days = trigger.Days,
        StartTime = trigger.StartTime,
        EndTime = trigger.EndTime,
        ActiveFrom = trigger.ActiveFrom,
        ExpiresAt = trigger.ExpiresAt,
        CreatedAt = trigger.CreatedAt,
    };

    public static CaptureRule Clone(CaptureRule rule) => new()
    {
        Id = rule.Id,
        TenantId = rule.TenantId,
        Name = rule.Name,
        Enabled = rule.Enabled,
        Classes = [.. rule.Classes],
        ClassColors = new Dictionary<string, string>(rule.ClassColors, StringComparer.OrdinalIgnoreCase),
        ConfidenceThreshold = rule.ConfidenceThreshold,
        MaxSegmentSeconds = rule.MaxSegmentSeconds,
        LingerSeconds = rule.LingerSeconds,
        GroupWindowMinutes = rule.GroupWindowMinutes,
        ActiveFrom = rule.ActiveFrom,
        ActiveTo = rule.ActiveTo,
        CreatedAt = rule.CreatedAt,
        Triggers = rule.Triggers.Select(Clone).ToList(),
    };
}
