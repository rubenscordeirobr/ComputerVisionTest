namespace CameraVision.Core.Entities;

/// <summary>How the editor presents a trigger. Evaluation (IsActiveAt) is uniform.</summary>
public enum AlertTriggerKind
{
    Always = 0,
    Weekly = 1,
    Temporary = 2,
}

[Flags]
public enum DaysOfWeek
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    All = Weekdays | Weekend,
}

public static class DaysOfWeekExtensions
{
    /// <summary>Monday = 1 … Sunday = 64 (System.DayOfWeek starts on Sunday).</summary>
    public static DaysOfWeek ToFlag(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => DaysOfWeek.Monday,
        DayOfWeek.Tuesday => DaysOfWeek.Tuesday,
        DayOfWeek.Wednesday => DaysOfWeek.Wednesday,
        DayOfWeek.Thursday => DaysOfWeek.Thursday,
        DayOfWeek.Friday => DaysOfWeek.Friday,
        DayOfWeek.Saturday => DaysOfWeek.Saturday,
        DayOfWeek.Sunday => DaysOfWeek.Sunday,
        _ => DaysOfWeek.None,
    };

    public static bool Contains(this DaysOfWeek days, DayOfWeek day) => (days & day.ToFlag()) != 0;
}

/// <summary>
/// One notification of a capture rule: a channel, the contacts to notify and when the
/// trigger applies. Kind only drives the editor; IsActiveAt evaluates every constraint
/// (enabled, validity bounds, days, time window) uniformly, so a temporary notice may
/// also carry a weekly schedule.
/// </summary>
public class AlertTrigger
{
    public int Id { get; set; }
    public int CaptureRuleId { get; set; }
    public bool Enabled { get; set; } = true;
    public AlertChannel Channel { get; set; }

    /// <summary>Contacts to notify; a contact without an address for Channel is skipped.</summary>
    public List<int> ContactIds { get; set; } = [];

    public AlertTriggerKind Kind { get; set; } = AlertTriggerKind.Always;
    public DaysOfWeek Days { get; set; } = DaysOfWeek.All;

    /// <summary>
    /// Both null = all day. EndTime &lt;= StartTime crosses midnight: 18:00–00:00 runs
    /// until the end of the day, 22:00–06:00 spills into the next morning and equal
    /// times mean a 24 h window starting at StartTime.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Validity bounds (local time). Temporary notices set ActiveFrom at activation;
    /// ExpiresAt null = valid until disabled or deleted.
    /// </summary>
    public DateTime? ActiveFrom { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsAllDay => StartTime == null || EndTime == null;

    public bool CrossesMidnight => StartTime is { } start && EndTime is { } end && end <= start;

    public bool IsExpiredAt(DateTime now) => ExpiresAt is { } until && until <= now;

    /// <summary>
    /// Whether the trigger applies at <paramref name="moment"/> (the capture's start).
    /// For windows crossing midnight the day of week is the day the window STARTED:
    /// "Friday 22:00–06:00" covers Saturday 02:00, while "Mon–Fri 00:00–06:00" does not
    /// cover Saturday 03:00.
    /// </summary>
    public bool IsActiveAt(DateTime moment)
    {
        if (!Enabled)
            return false;
        if (ActiveFrom is { } from && moment < from)
            return false;
        if (ExpiresAt is { } until && moment >= until)
            return false;
        if (Days == DaysOfWeek.None)
            return false;

        if (StartTime is not { } start || EndTime is not { } end)
            return Days.Contains(moment.DayOfWeek);

        var time = TimeOnly.FromDateTime(moment);
        if (start < end)
            return time >= start && time < end && Days.Contains(moment.DayOfWeek);

        // Crosses midnight: the part after Start belongs to today's window, the part
        // before End to the window that started yesterday.
        if (time >= start)
            return Days.Contains(moment.DayOfWeek);
        if (time < end)
            return Days.Contains(moment.AddDays(-1).DayOfWeek);
        return false;
    }
}
