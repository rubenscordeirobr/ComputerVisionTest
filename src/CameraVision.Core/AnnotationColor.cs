using System.Diagnostics.CodeAnalysis;

namespace CameraVision.Core;

/// <summary>
/// Annotation colors are stored as "#RRGGBB" (upper-case). Accepts what an HTML color
/// input or a user may type: with or without "#", 3 or 6 hex digits, any case.
/// </summary>
public static class AnnotationColor
{
    public static bool TryNormalize(string? value, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var hex = value.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
            return false;

        normalized = "#" + hex.ToUpperInvariant();
        return true;
    }

    /// <summary>Drops empty or malformed entries and normalizes the rest.</summary>
    public static Dictionary<string, string> Sanitize(IEnumerable<KeyValuePair<string, string?>> colors)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (className, value) in colors)
        {
            if (!string.IsNullOrWhiteSpace(className) && TryNormalize(value, out var hex))
                result[className] = hex;
        }
        return result;
    }
}
