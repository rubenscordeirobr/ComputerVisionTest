using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CameraVision.Core.Commands;

/// <summary>
/// Tier 1 of the command interpreter: accent-insensitive PT-BR keyword rules. They
/// answer the plain phrasings ("ativar os alertas", "desligue os avisos", "por 2
/// horas") and return null for anything ambiguous, which is then handed to the LLM.
/// </summary>
public static partial class CommandTextRules
{
    private const int ShortMessageWords = 4;

    [GeneratedRegex(@"\b(?:re)?ativ\w*|\blig\w*|\bhabilit\w*|\binici\w*|\bcomec\w*|\bquero\s+receber\b")]
    private static partial Regex EnableVerbs();

    [GeneratedRegex(@"\bdesativ\w*|\bdeslig\w*|\bdesabilit\w*|\bpar(?:ar|e|em)\b|\bcancel\w*|\bencerr\w*|\bsuspend\w*|\bsilenci\w*|\bnao\s+quero\b")]
    private static partial Regex DisableVerbs();

    [GeneratedRegex(@"\balert\w*|\bavis\w*|\bnotific\w*")]
    private static partial Regex Nouns();

    [GeneratedRegex(@"\bnao\b")]
    private static partial Regex Negation();

    /// <summary>Lower-case ASCII: accents stripped, punctuation turned into spaces, one space between words.</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(char.IsLetterOrDigit(ch) || ch == ':' ? char.ToLowerInvariant(ch) : ' ');
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Null = the rules cannot decide. When <paramref name="expectingDuration"/> is set
    /// (the agent just asked "até quando?") a bare validity such as "2 horas" is a
    /// <see cref="CommandIntent.SetDuration"/>.
    /// </summary>
    public static CommandInterpretation? TryMatch(string text, DateTime now, bool expectingDuration = false)
    {
        var folded = Fold(text);
        if (folded.Length == 0)
            return null;

        var duration = CommandDurationRules.TryParse(folded, now);
        var command = CommandDurationRules.RemoveOpenEnded(folded);
        var enable = EnableVerbs().IsMatch(command);
        var disable = DisableVerbs().IsMatch(command);

        if (enable && disable)
            return null;
        if (enable && Negation().IsMatch(command))
            return null;

        var words = folded.Split(' ').Length;
        var aboutAlerts = Nouns().IsMatch(folded) || words <= ShortMessageWords;

        if (enable && aboutAlerts)
            return new CommandInterpretation(CommandIntent.EnableAlerts, duration?.Until, duration?.UntilDisabled ?? false);
        if (disable && aboutAlerts)
            return new CommandInterpretation(CommandIntent.DisableAlerts);
        if (expectingDuration && duration != null && !enable && !disable)
            return new CommandInterpretation(CommandIntent.SetDuration, duration.Until, duration.UntilDisabled);
        return null;
    }
}
