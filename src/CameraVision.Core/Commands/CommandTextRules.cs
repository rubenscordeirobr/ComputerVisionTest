using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CameraVision.Core.Commands;

/// <summary>
/// Tier 1 of the command interpreter: accent-insensitive PT-BR keyword rules. They
/// answer the plain phrasings ("ativar os alertas", "desligue os avisos", "por 2
/// horas", "status", "últimas 5 capturas de pessoas") and return null for anything
/// ambiguous, which is then handed to the LLM.
/// </summary>
public static partial class CommandTextRules
{
    private const int ShortMessageWords = 4;

    [GeneratedRegex(@"\b(?:re)?ativ\w*|\blig(?:a|ar|ue|uem|o)\b|\bhabilit\w*|\binici\w*|\bcomec\w*|\bquero\s+receber\b")]
    private static partial Regex EnableVerbs();

    [GeneratedRegex(@"\bdesativ\w*|\bdeslig(?:a|ar|ue|uem|o)\b|\bdesabilit\w*|\bpar(?:ar|e|em)\b|\bcancel\w*|\bencerr\w*|\bsuspend\w*|\bsilenci\w*|\bnao\s+quero\b")]
    private static partial Regex DisableVerbs();

    [GeneratedRegex(@"\balert\w*|\bavis\w*|\bnotific\w*")]
    private static partial Regex Nouns();

    [GeneratedRegex(@"\bnao\b")]
    private static partial Regex Negation();

    // "status", "saúde" (and the "sudade" typo), "situação", "como estão", "funcionando", "ligadas/online".
    [GeneratedRegex(@"\bstatus\b|\bsa?ud(?:ade|e)\b|\bsituac\w*|\bestado\b|\bcomo\s+(?:esta|estao|ta|tao|vao|vai|anda|andam)\b|\bfuncion\w*|\bligad[ao]s?\b|\bon\s*line\b|\boff\s*line\b|\bfora\s+do\s+ar\b|\bcaiu\b|\bcairam\b")]
    private static partial Regex StatusCues();

    [GeneratedRegex(@"\bcam[ae]ras?\b|\bprocessador\b|\bsistema\b|\bdetec\w*|\bworker\b|\bgravador\b")]
    private static partial Regex StatusNouns();

    [GeneratedRegex(@"\bcapturas?\b|\bgravac\w*|\bvideos?\b|\bdeteccoes\b|\bdeteccao\b|\bregistros?\b")]
    private static partial Regex CaptureNouns();

    [GeneratedRegex(@"\bultim\w*|\blist\w*|\benvi\w*|\bmand\w*|\bmostr\w*|\bver\b|\bquais\b|\bquero\b|\bpass\w*|\brecentes?\b|\bme\s+d[ae]\b")]
    private static partial Regex ListVerbs();

    [GeneratedRegex(@"\b(\d{1,3})\b")]
    private static partial Regex Number();

    [GeneratedRegex(@"\b(uma|um|duas|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\b")]
    private static partial Regex WordNumber();

    // The object phrase after the capture noun: "capturas de pessoas de hoje" → "pessoas".
    [GeneratedRegex(@"\b(?:de|das|dos|do|da|com)\s+(.+?)(?:\s+(?:de\s+)?(?:hoje|ontem|agora|recentes?)\b|\s+por\s+favor\b|\s+pf\b|$)")]
    private static partial Regex ClassPhrase();

    [GeneratedRegex(@"^(?:hoje|ontem|agora|recentes?|todas?|tudo)$")]
    private static partial Regex TimeWords();

    // Answers to "Quer que eu …? Responda \"sim\"." — only consulted while an offer is pending (SPEC-20).
    [GeneratedRegex(@"\bsim\b|\bs\b|\bok(?:ay)?\b|\bpode\b|\bclaro\b|\bquero\b|\bmand\w*|\benvi\w*|\bisso\b|\bbora\b|\bbeleza\b|\bblz\b|\bpositivo\b|\bcom\s+certeza\b|\bfavor\b|\bpfv?\b|\bta\b|\btudo\s+bem\b|\baceito\b|\byes\b|\bvai\b|\bfaz\b")]
    private static partial Regex YesWords();

    [GeneratedRegex(@"\bnao\b|\bn\b|\bdeixa\b|\bdepois\b|\bnegativo\b|\bdispens\w*|\bnem\b|\bcancel\w*")]
    private static partial Regex NoWords();

    private static readonly string[] YesEmojis = ["👍", "👌", "✅"];

    // Words that may sit between the class and a qualifier without carrying meaning of their own.
    private static readonly HashSet<string> Connectors = ["de", "da", "do", "das", "dos", "e", "com", "a", "o", "as", "os"];

    private static readonly Dictionary<string, int> WordNumbers = new()
    {
        ["uma"] = 1, ["um"] = 1, ["duas"] = 2, ["dois"] = 2, ["tres"] = 3, ["quatro"] = 4, ["cinco"] = 5,
        ["seis"] = 6, ["sete"] = 7, ["oito"] = 8, ["nove"] = 9, ["dez"] = 10,
    };

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

        if (TryMatchCaptures(folded, words) is { } captures)
            return captures;
        if (StatusCues().IsMatch(folded) && (StatusNouns().IsMatch(folded) || words <= ShortMessageWords))
            return new CommandInterpretation(CommandIntent.CameraStatus);

        if (expectingDuration && duration != null && !enable && !disable)
            return new CommandInterpretation(CommandIntent.SetDuration, duration.Until, duration.UntilDisabled);
        return null;
    }

    /// <summary>
    /// Yes/no to the command the agent offered (SPEC-20): true = run it, false = drop it,
    /// null = the message is something else and goes through the normal interpretation.
    /// Only short messages count, a message that names alerts, status or captures is a
    /// command in its own right, and a "não" wins over any yes word ("não, obrigado").
    /// </summary>
    public static bool? TryMatchConfirmation(string text)
    {
        var thumbsUp = YesEmojis.Any(text.Contains);
        var folded = Fold(text);
        if (folded.Length == 0)
            return thumbsUp ? true : null;
        if (folded.Split(' ').Length > ShortMessageWords ||
            Nouns().IsMatch(folded) || StatusCues().IsMatch(folded) || CaptureNouns().IsMatch(folded))
            return null;
        if (NoWords().IsMatch(folded))
            return false;
        return YesWords().IsMatch(folded) || thumbsUp ? true : null;
    }

    /// <summary>"últimas 3 capturas de pessoas" → ListCaptures, Count 3, person. Count is the raw number (clamped later).</summary>
    private static CommandInterpretation? TryMatchCaptures(string folded, int words)
    {
        var noun = CaptureNouns().Match(folded);
        if (!noun.Success || !(ListVerbs().IsMatch(folded) || words <= 3))
            return null;

        int? count = null;
        if (Number().Match(folded) is { Success: true } digits)
            count = int.Parse(digits.Groups[1].Value);
        else if (WordNumber().Match(folded) is { Success: true } word)
            count = WordNumbers[word.Groups[1].Value];

        string? objectClass = null;
        string? unknownClass = null;
        var tentative = false;
        var after = folded[(noun.Index + noun.Length)..];
        if (ClassPhrase().Match(after) is { Success: true } phrase)
        {
            var candidate = phrase.Groups[1].Value.Trim();
            var parts = candidate.Split(' ');
            var matched = DetectableClassResolver.TryResolve(candidate) != null
                ? candidate
                : parts.FirstOrDefault(p => DetectableClassResolver.TryResolve(p) != null);
            objectClass = matched == null ? null : DetectableClassResolver.TryResolve(matched);
            if (objectClass == null && candidate.Length > 0 && !TimeWords().IsMatch(candidate))
                unknownClass = candidate;
            // "pessoas de camisa amarela", "carros na garagem": a class plus words the rules
            // cannot read (or no known class at all) — a model should look before the reply goes out.
            tentative = unknownClass != null ||
                        (matched != null && matched != candidate &&
                         parts.Any(p => p != matched && !Connectors.Contains(p) && !TimeWords().IsMatch(p)));
        }

        return new CommandInterpretation(CommandIntent.ListCaptures, Count: count, ObjectClass: objectClass,
            UnknownClass: unknownClass, Tentative: tentative);
    }
}
