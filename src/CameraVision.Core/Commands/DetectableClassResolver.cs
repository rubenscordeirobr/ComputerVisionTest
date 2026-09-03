namespace CameraVision.Core.Commands;

/// <summary>
/// Maps the object word a sender uses ("pessoas", "cães", "carro", "person") to the
/// COCO class name the captures are stored with. Accent-insensitive, singularizes
/// PT-BR plurals and knows a few synonyms the PT-BR labels do not cover.
/// </summary>
public static class DetectableClassResolver
{
    private static readonly Dictionary<string, string> Synonyms = new()
    {
        ["pessoa"] = "person", ["gente"] = "person", ["humano"] = "person", ["pessoal"] = "person",
        ["alguem"] = "person", ["invasor"] = "person", ["visitante"] = "person", ["homem"] = "person",
        ["mulher"] = "person", ["crianca"] = "person",
        ["cachorro"] = "dog", ["cao"] = "dog", ["cadela"] = "dog",
        ["veiculo"] = "car", ["automovel"] = "car",
        ["motocicleta"] = "motorcycle",
        ["caminhao"] = "truck", ["caminhonete"] = "truck",
        ["onibus"] = "bus",
        ["passaro"] = "bird", ["ave"] = "bird",
        ["bike"] = "bicycle", ["bicicleta"] = "bicycle",
        ["gato"] = "cat", ["gata"] = "cat",
    };

    /// <summary>Folded PT-BR label → COCO name ("passaro" → "bird").</summary>
    private static readonly Dictionary<string, string> ByLabel = BuildLabelIndex();

    /// <summary>The COCO class name, or null when the word is not a detectable object.</summary>
    public static string? TryResolve(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return null;
        var folded = CommandTextRules.Fold(word);
        if (folded.Length == 0)
            return null;

        foreach (var candidate in new[] { folded, Singular(folded) })
        {
            if (Synonyms.TryGetValue(candidate, out var bySynonym))
                return bySynonym;
            if (ByLabel.TryGetValue(candidate, out var byLabel))
                return byLabel;
            var english = DetectableClasses.Names.FirstOrDefault(n =>
                n.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
                (n + "s").Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (english != null)
                return english;
        }
        return null;
    }

    /// <summary>PT-BR plural → singular on folded text: "caes" → "cao", "caminhoes" → "caminhao", "carros" → "carro".</summary>
    public static string Singular(string folded)
    {
        if (folded.EndsWith("oes") || folded.EndsWith("aes"))
            return folded[..^3] + "ao";
        if (folded.EndsWith("ais"))
            return folded[..^3] + "al";
        if (folded.EndsWith("eis"))
            return folded[..^3] + "el";
        if (folded.EndsWith("ns"))
            return folded[..^2] + "m";
        if (folded.EndsWith("res") || folded.EndsWith("zes"))
            return folded[..^2];
        if (folded.EndsWith('s') && folded.Length > 3)
            return folded[..^1];
        return folded;
    }

    private static Dictionary<string, string> BuildLabelIndex()
    {
        var index = new Dictionary<string, string>();
        foreach (var name in DetectableClasses.Names)
            index.TryAdd(CommandTextRules.Fold(DetectableClasses.Translate(name)), name);
        return index;
    }
}
