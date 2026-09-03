namespace CameraVision.Core;

/// <summary>PT-BR relative/duration text for status displays.</summary>
public static class TimeText
{
    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        if (span.TotalSeconds < 60)
            return $"{(int)span.TotalSeconds} s";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes} min";
        if (span.TotalHours < 24)
            return span.Minutes == 0 ? $"{(int)span.TotalHours} h" : $"{(int)span.TotalHours} h {span.Minutes} min";
        return span.Hours == 0 ? $"{(int)span.TotalDays} d" : $"{(int)span.TotalDays} d {span.Hours} h";
    }

    public static string Ago(DateTime from, DateTime now) => $"há {Duration(now - from)}";
}
