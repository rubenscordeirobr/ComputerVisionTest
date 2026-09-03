using System.Text.RegularExpressions;

namespace CameraVision.Core.Commands;

/// <summary>Validity requested in a message. Until null + UntilDisabled = "até eu desativar".</summary>
public sealed record CommandDuration(DateTime? Until, bool UntilDisabled)
{
    public static readonly CommandDuration Open = new(null, true);
}

/// <summary>
/// Parses the validity part of a PT-BR command over folded text (see
/// <see cref="CommandTextRules.Fold"/>): "por 2 horas", "até as 22h", "até 22:30",
/// "até amanhã", "hoje", "até eu desativar". Clock times already past today mean
/// tomorrow; "amanhã" alone means tomorrow 08:00.
/// </summary>
public static partial class CommandDurationRules
{
    private static readonly Dictionary<string, int> WordNumbers = new()
    {
        ["uma"] = 1, ["um"] = 1, ["duas"] = 2, ["dois"] = 2, ["tres"] = 3, ["quatro"] = 4, ["cinco"] = 5,
        ["seis"] = 6, ["sete"] = 7, ["oito"] = 8, ["nove"] = 9, ["dez"] = 10, ["doze"] = 12, ["vinte e quatro"] = 24,
    };

    [GeneratedRegex(@"\bate\s+(?:eu\s+)?(?:desativar|desligar|cancelar|mandar|avisar|falar)\b|\bsem\s+prazo\b|\bate\s+segunda\s+ordem\b|\bpor\s+tempo\s+indeterminado\b")]
    private static partial Regex OpenEnded();

    [GeneratedRegex(@"\bamanha(?:\s+(?:as|de\s+manha\s+as)?\s*(\d{1,2})(?::(\d{2})|h(\d{2})?)?)?\b")]
    private static partial Regex Tomorrow();

    // "ate as 22", "ate as 22:30", "ate as 22h", "ate 22h", "ate 22:30", "ate 22h30" — never "ate 2 horas".
    [GeneratedRegex(@"\bate\s+(?:as\s+(\d{1,2})(?::(\d{2}))?\s*h?|(\d{1,2})(?::(\d{2})|h(\d{2})?))\b")]
    private static partial Regex UntilClock();

    [GeneratedRegex(@"\b(?:ate\s+(?:a\s+)?)?meia\s*noite\b")]
    private static partial Regex Midnight();

    [GeneratedRegex(@"\b(?:ate\s+(?:o\s+)?)?meio\s*dia\b")]
    private static partial Regex Noon();

    [GeneratedRegex(@"\b(\d{1,3})\s*(?:h|hr|hrs|hora|horas)\b")]
    private static partial Regex Hours();

    [GeneratedRegex(@"\b(uma|um|duas|dois|tres|quatro|cinco|seis|sete|oito|nove|dez|doze|vinte e quatro)\s+horas?\b")]
    private static partial Regex WordHours();

    [GeneratedRegex(@"\bmeia\s+hora\b")]
    private static partial Regex HalfHour();

    [GeneratedRegex(@"\b(\d{1,3})\s*(?:min|mins|minutos?)\b")]
    private static partial Regex Minutes();

    [GeneratedRegex(@"\b(\d{1,2})\s*dias?\b")]
    private static partial Regex Days();

    [GeneratedRegex(@"\b(?:um|1)\s+dia\b")]
    private static partial Regex OneDay();

    [GeneratedRegex(@"\bhoje\b|\bfim\s+do\s+dia\b|\bfinal\s+do\s+dia\b|\bo\s+dia\s+todo\b")]
    private static partial Regex Today();

    /// <summary>The text without its "até eu desativar" clause, so its verb is not read as a command.</summary>
    public static string RemoveOpenEnded(string folded) => OpenEnded().Replace(folded, " ");

    public static CommandDuration? TryParse(string folded, DateTime now)
    {
        if (OpenEnded().IsMatch(folded))
            return CommandDuration.Open;

        if (Tomorrow().Match(folded) is { Success: true } tomorrow)
        {
            var day = now.Date.AddDays(1);
            if (tomorrow.Groups[1].Success)
            {
                var hour = int.Parse(tomorrow.Groups[1].Value);
                var minute = FirstInt(tomorrow.Groups[2], tomorrow.Groups[3]);
                if (hour is < 0 or > 23 || minute is < 0 or > 59)
                    return null;
                return new CommandDuration(day.AddHours(hour).AddMinutes(minute), false);
            }
            return new CommandDuration(day.AddHours(8), false);
        }

        if (UntilClock().Match(folded) is { Success: true } clock)
        {
            var hour = int.Parse(clock.Groups[1].Success ? clock.Groups[1].Value : clock.Groups[3].Value);
            var minute = FirstInt(clock.Groups[2], clock.Groups[4], clock.Groups[5]);
            if (hour is < 0 or > 23 || minute is < 0 or > 59)
                return null;
            return new CommandDuration(NextOccurrence(now, hour, minute), false);
        }

        if (Midnight().IsMatch(folded))
            return new CommandDuration(now.Date.AddDays(1), false);
        if (Noon().IsMatch(folded))
            return new CommandDuration(NextOccurrence(now, 12, 0), false);

        if (Hours().Match(folded) is { Success: true } hours)
            return new CommandDuration(now.AddHours(int.Parse(hours.Groups[1].Value)), false);
        if (WordHours().Match(folded) is { Success: true } wordHours)
            return new CommandDuration(now.AddHours(WordNumbers[wordHours.Groups[1].Value]), false);
        if (HalfHour().IsMatch(folded))
            return new CommandDuration(now.AddMinutes(30), false);
        if (Minutes().Match(folded) is { Success: true } minutes)
            return new CommandDuration(now.AddMinutes(int.Parse(minutes.Groups[1].Value)), false);
        if (Days().Match(folded) is { Success: true } days)
            return new CommandDuration(now.AddDays(int.Parse(days.Groups[1].Value)), false);
        if (OneDay().IsMatch(folded))
            return new CommandDuration(now.AddDays(1), false);
        if (Today().IsMatch(folded))
            return new CommandDuration(now.Date.AddDays(1), false);

        return null;
    }

    private static int FirstInt(params Group[] groups)
    {
        foreach (var group in groups)
            if (group.Success)
                return int.Parse(group.Value);
        return 0;
    }

    /// <summary>Today at the given time, or tomorrow when that moment has already passed.</summary>
    private static DateTime NextOccurrence(DateTime now, int hour, int minute)
    {
        var candidate = now.Date.AddHours(hour).AddMinutes(minute);
        return candidate <= now ? candidate.AddDays(1) : candidate;
    }
}
