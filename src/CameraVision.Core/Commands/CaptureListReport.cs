using CameraVision.Core.Entities;

namespace CameraVision.Core.Commands;

/// <summary>PT-BR text of the "últimas N capturas" answer, one line per capture with its watch link.</summary>
public static class CaptureListReport
{
    public static string Compose(IReadOnlyList<Capture> items, int total, int requested, string? objectClass,
        Func<Capture, string> link)
    {
        var subject = objectClass == null ? "" : $" de {DetectableClasses.Translate(objectClass)}";
        if (items.Count == 0)
            return $"Nenhuma captura{subject} encontrada.";

        var lines = new List<string> { $"Últimas {items.Count} capturas{subject} — CameraVision" };
        for (var i = 0; i < items.Count; i++)
        {
            var capture = items[i];
            var label = objectClass == null ? $" · {DetectableClasses.Translate(capture.ObjectClass)}" : "";
            lines.Add($"{i + 1}. {capture.StartedAt:dd/MM HH:mm} · {capture.CameraName}{label} · " +
                      $"{capture.Duration:mm\\:ss} · {link(capture)}");
        }

        if (total > items.Count)
        {
            var more = items.Count < CommandInterpretation.MaxCount
                ? $" Envie \"últimas {CommandInterpretation.MaxCount} capturas{subject}\" para ver mais."
                : "";
            lines.Add("");
            lines.Add($"Mostrando {items.Count} de {total}.{more}");
        }
        if (requested > CommandInterpretation.MaxCount)
            lines.Add($"(O máximo por mensagem é {CommandInterpretation.MaxCount}.)");
        return string.Join('\n', lines);
    }

    /// <summary>The sender named an object the detector does not know.</summary>
    public static string UnknownClass(string word) =>
        $"Não reconheci \"{word}\" como um objeto detectável. Exemplos: pessoas, gatos, cachorros, carros, motos, caminhões, pássaros.";
}
