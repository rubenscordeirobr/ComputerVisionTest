using CameraVision.Core.Entities;

namespace CameraVision.Core.Tests;

public class AlertTriggerTests
{
    // 2026-09-07 is a Monday; every test date is built from it.
    private static readonly DateTime Monday = new(2026, 9, 7);

    private static DateTime On(DayOfWeek day, int hour, int minute = 0, int second = 0) =>
        Monday.AddDays(((int)day + 6) % 7).AddHours(hour).AddMinutes(minute).AddSeconds(second);

    private static AlertTrigger Weekly(DaysOfWeek days, (int h, int m)? start = null, (int h, int m)? end = null) => new()
    {
        Kind = AlertTriggerKind.Weekly,
        Days = days,
        StartTime = start is { } s ? new TimeOnly(s.h, s.m) : null,
        EndTime = end is { } e ? new TimeOnly(e.h, e.m) : null,
    };

    [Fact]
    public void Anchor_date_is_a_monday() => Assert.Equal(DayOfWeek.Monday, Monday.DayOfWeek);

    [Fact]
    public void Always_is_active_at_any_moment()
    {
        var trigger = new AlertTrigger();
        Assert.True(trigger.IsActiveAt(On(DayOfWeek.Sunday, 3)));
        Assert.True(trigger.IsActiveAt(On(DayOfWeek.Wednesday, 15, 30)));
    }

    [Fact]
    public void Disabled_trigger_is_never_active()
    {
        var trigger = new AlertTrigger { Enabled = false };
        Assert.False(trigger.IsActiveAt(On(DayOfWeek.Wednesday, 15)));
    }

    [Fact]
    public void No_days_selected_is_never_active()
    {
        var trigger = Weekly(DaysOfWeek.None);
        Assert.False(trigger.IsActiveAt(On(DayOfWeek.Wednesday, 15)));
    }

    [Theory]
    [InlineData(DayOfWeek.Tuesday, 3, 0, 0, true)]
    [InlineData(DayOfWeek.Tuesday, 5, 59, 59, true)]
    [InlineData(DayOfWeek.Tuesday, 6, 0, 0, false)]
    [InlineData(DayOfWeek.Monday, 0, 0, 0, true)]
    [InlineData(DayOfWeek.Saturday, 3, 0, 0, false)]
    [InlineData(DayOfWeek.Friday, 12, 0, 0, false)]
    public void Weekday_night_window(DayOfWeek day, int hour, int minute, int second, bool expected)
    {
        var trigger = Weekly(DaysOfWeek.Weekdays, (0, 0), (6, 0));
        Assert.Equal(expected, trigger.IsActiveAt(On(day, hour, minute, second)));
    }

    [Theory]
    [InlineData(DayOfWeek.Friday, 23, 59, false)]
    [InlineData(DayOfWeek.Saturday, 0, 0, true)]
    [InlineData(DayOfWeek.Saturday, 12, 0, true)]
    [InlineData(DayOfWeek.Sunday, 23, 59, true)]
    [InlineData(DayOfWeek.Monday, 0, 0, false)]
    public void Weekend_all_day(DayOfWeek day, int hour, int minute, bool expected)
    {
        var trigger = Weekly(DaysOfWeek.Weekend);
        Assert.Equal(expected, trigger.IsActiveAt(On(day, hour, minute)));
    }

    [Theory]
    [InlineData(DayOfWeek.Tuesday, 17, 59, 59, false)]
    [InlineData(DayOfWeek.Tuesday, 18, 0, 0, true)]
    [InlineData(DayOfWeek.Tuesday, 23, 59, 59, true)]
    [InlineData(DayOfWeek.Wednesday, 0, 0, 0, false)]
    public void Evening_until_midnight(DayOfWeek day, int hour, int minute, int second, bool expected)
    {
        // "18:00 até 00:00" = 18:00 until the end of the day.
        var trigger = Weekly(DaysOfWeek.All, (18, 0), (0, 0));
        Assert.Equal(expected, trigger.IsActiveAt(On(day, hour, minute, second)));
    }

    [Theory]
    [InlineData(DayOfWeek.Friday, 23, 0, true)]
    [InlineData(DayOfWeek.Saturday, 3, 0, true)]   // the window that started on Friday
    [InlineData(DayOfWeek.Saturday, 6, 0, false)]
    [InlineData(DayOfWeek.Saturday, 23, 0, false)] // Saturday is not selected
    [InlineData(DayOfWeek.Friday, 3, 0, false)]    // Thursday's window, not selected
    [InlineData(DayOfWeek.Sunday, 3, 0, false)]
    public void Window_crossing_midnight_belongs_to_the_day_it_started(DayOfWeek day, int hour, int minute, bool expected)
    {
        var trigger = Weekly(DaysOfWeek.Friday, (22, 0), (6, 0));
        Assert.Equal(expected, trigger.IsActiveAt(On(day, hour, minute)));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 5, 59, false)]
    [InlineData(DayOfWeek.Monday, 6, 0, true)]
    [InlineData(DayOfWeek.Tuesday, 5, 59, true)]
    [InlineData(DayOfWeek.Tuesday, 6, 0, false)]
    public void Equal_times_mean_a_full_day_starting_at_that_time(DayOfWeek day, int hour, int minute, bool expected)
    {
        var trigger = Weekly(DaysOfWeek.Monday, (6, 0), (6, 0));
        Assert.Equal(expected, trigger.IsActiveAt(On(day, hour, minute)));
    }

    [Fact]
    public void Temporary_respects_its_validity_bounds()
    {
        var trigger = new AlertTrigger
        {
            Kind = AlertTriggerKind.Temporary,
            ActiveFrom = On(DayOfWeek.Tuesday, 14),
            ExpiresAt = On(DayOfWeek.Wednesday, 8),
        };
        Assert.False(trigger.IsActiveAt(On(DayOfWeek.Tuesday, 13, 59)));
        Assert.True(trigger.IsActiveAt(On(DayOfWeek.Tuesday, 14)));
        Assert.True(trigger.IsActiveAt(On(DayOfWeek.Wednesday, 7, 59)));
        Assert.False(trigger.IsActiveAt(On(DayOfWeek.Wednesday, 8)));
        Assert.False(trigger.IsExpiredAt(On(DayOfWeek.Wednesday, 7, 59)));
        Assert.True(trigger.IsExpiredAt(On(DayOfWeek.Wednesday, 8)));
    }

    [Fact]
    public void Open_ended_temporary_stays_active_until_disabled()
    {
        var trigger = new AlertTrigger { Kind = AlertTriggerKind.Temporary, ActiveFrom = On(DayOfWeek.Tuesday, 14) };
        Assert.True(trigger.IsActiveAt(On(DayOfWeek.Tuesday, 14).AddDays(30)));
        Assert.False(trigger.IsExpiredAt(On(DayOfWeek.Tuesday, 14).AddDays(30)));
        trigger.Enabled = false;
        Assert.False(trigger.IsActiveAt(On(DayOfWeek.Tuesday, 15)));
    }

    [Fact]
    public void Temporary_can_also_carry_a_weekly_schedule()
    {
        var trigger = Weekly(DaysOfWeek.Weekdays, (0, 0), (6, 0));
        trigger.Kind = AlertTriggerKind.Temporary;
        trigger.ExpiresAt = On(DayOfWeek.Wednesday, 12);
        Assert.True(trigger.IsActiveAt(On(DayOfWeek.Tuesday, 3)));
        Assert.False(trigger.IsActiveAt(On(DayOfWeek.Tuesday, 12)));   // outside the hours
        Assert.False(trigger.IsActiveAt(On(DayOfWeek.Thursday, 3)));  // expired
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, DaysOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday, DaysOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday, DaysOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday, DaysOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday, DaysOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday, DaysOfWeek.Saturday)]
    [InlineData(DayOfWeek.Sunday, DaysOfWeek.Sunday)]
    public void Day_flags_map_each_day_of_week(DayOfWeek day, DaysOfWeek expected)
    {
        Assert.Equal(expected, day.ToFlag());
        Assert.True(DaysOfWeek.All.Contains(day));
        Assert.Equal(expected is DaysOfWeek.Saturday or DaysOfWeek.Sunday, DaysOfWeek.Weekend.Contains(day));
    }

    [Fact]
    public void Weekdays_and_weekend_partition_the_week() =>
        Assert.Equal(DaysOfWeek.All, DaysOfWeek.Weekdays | DaysOfWeek.Weekend);

    [Fact]
    public void Running_temporary_requires_kind_enabled_and_validity()
    {
        var now = On(DayOfWeek.Wednesday, 15);
        var running = new AlertTrigger
        {
            Kind = AlertTriggerKind.Temporary, ActiveFrom = now.AddHours(-1), ExpiresAt = now.AddHours(1),
        };
        Assert.True(running.IsRunningTemporaryAt(now));

        Assert.False(new AlertTrigger { Kind = AlertTriggerKind.Temporary, ActiveFrom = now.AddHours(-2), ExpiresAt = now.AddHours(-1) }
            .IsRunningTemporaryAt(now));
        Assert.False(new AlertTrigger { Kind = AlertTriggerKind.Temporary, ActiveFrom = now.AddHours(-1), Enabled = false }
            .IsRunningTemporaryAt(now));
        Assert.False(new AlertTrigger { Kind = AlertTriggerKind.Always }.IsRunningTemporaryAt(now));
        Assert.True(new AlertTrigger { Kind = AlertTriggerKind.Temporary, ActiveFrom = now.AddHours(-1) }
            .IsRunningTemporaryAt(now));
    }
}
