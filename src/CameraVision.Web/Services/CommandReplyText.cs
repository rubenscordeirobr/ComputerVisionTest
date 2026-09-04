using CameraVision.Core;
using CameraVision.Core.Commands;

namespace CameraVision.Web.Services;

/// <summary>PT-BR replies of the WhatsApp command agent, in one place.</summary>
public static class CommandReplyText
{
    public const string AskUntilWhen = "Até quando? (ex.: \"2 horas\", \"até as 22h\", \"até amanhã\", \"até eu desativar\")";

    private const string HelpLines =
        "• \"ativar alertas\" / \"desativar alertas\" — receber as capturas no WhatsApp\n" +
        "• \"status\" — situação das câmeras e do processador\n" +
        "• \"últimas 5 capturas de pessoas\" — lista com links dos vídeos";

    public static string Enabled(int rules, int extended, DateTime? expiresAt, bool askUntilWhen)
    {
        var what = extended > 0 && extended == rules
            ? $"Seus alertas já estavam ativos — prorrogados em {rules} regra(s)"
            : $"Alertas por WhatsApp ativados em {rules} regra(s)";
        var until = expiresAt is { } at ? $" até {Stamp(at)}" : " até você desativar";
        var text = $"{what}{until}.";
        if (askUntilWhen)
            text += $"\n\n{AskUntilWhen}\nSem resposta, encerram às {Stamp(expiresAt!.Value)}.";
        else
            text += "\nEnvie \"desativar alertas\" para encerrar antes.";
        return text;
    }

    public static string NoRules() =>
        "Nenhuma regra de captura ativa foi encontrada para o seu cliente — configure as regras no painel primeiro.";

    public static string DurationSet(int rules, DateTime? expiresAt) =>
        expiresAt is { } at
            ? $"Combinado: alertas ativos em {rules} regra(s) até {Stamp(at)}."
            : $"Combinado: alertas ativos em {rules} regra(s) até você desativar.";

    public static string Disabled(int rules) =>
        rules > 0
            ? $"Alertas temporários encerrados em {rules} regra(s)."
            : "Não havia alertas temporários ativos para o seu número.";

    public static string Unknown() => "Não entendi. Comandos que eu atendo:\n" + HelpLines;

    /// <summary>
    /// Understood but not implemented (SPEC-20). With an offer the sender is asked to
    /// confirm the closest supported command; without one the help lines follow.
    /// </summary>
    public static string Unsupported(string request, string? offer) =>
        offer != null
            ? $"Entendi que você quer {request}. Ainda não tenho essa função, mas seria ótimo — " +
              $"anotei sua sugestão para a equipe! 📝\n\nQuer que eu {offer}? Responda \"sim\"."
            : $"Entendi que você quer {request}, porém isso ainda não está implementado. " +
              "Anotei sua sugestão para a equipe! 📝\n\nPor enquanto eu atendo:\n" + HelpLines;

    /// <summary>The offered command as the verb phrase of "Quer que eu …?"; null when there is nothing to offer.</summary>
    public static string? Offer(CommandInterpretation? fallback) => fallback switch
    {
        { Intent: CommandIntent.ListCaptures } list =>
            "envie as últimas " +
            (list.Count is { } count ? $"{Math.Clamp(count, 1, CommandInterpretation.MaxCount)} " : "") +
            "capturas" +
            (list.ObjectClass is { } objectClass ? $" de {DetectableClasses.Translate(objectClass)}" : ""),
        { Intent: CommandIntent.CameraStatus } => "informe o status das câmeras",
        { Intent: CommandIntent.EnableAlerts } => "ative os alertas no seu WhatsApp",
        { Intent: CommandIntent.DisableAlerts } => "desative os alertas",
        _ => null,
    };

    public static string Declined() => "Tudo bem! Se precisar, é só chamar. 👋";

    /// <summary>Prefix of every reply to a voice note, so the sender can spot a bad transcription.</summary>
    public static string Heard(string transcript) => $"🎤 Entendi: \"{transcript}\"\n\n";

    public static string AudioDisabled() =>
        "Comandos por áudio estão desativados — envie o pedido por texto.";

    public static string AudioTooLong(int maxSeconds) =>
        $"Áudio muito longo (máximo de {maxSeconds} s). Envie um áudio mais curto ou o pedido por texto.";

    public static string AudioNotUnderstood() =>
        "Não consegui entender o áudio. Tente de novo ou envie o pedido por texto.";

    public const string ChannelOffNote =
        "Obs.: o canal WhatsApp está desativado nas configurações de alertas do seu cliente — as mensagens só saem quando ele for ligado.";

    /// <summary>Intent label for the system pages.</summary>
    public static string IntentLabel(CommandIntent intent) => intent switch
    {
        CommandIntent.EnableAlerts => "Ativar alertas",
        CommandIntent.DisableAlerts => "Desativar alertas",
        CommandIntent.SetDuration => "Definir prazo",
        CommandIntent.CameraStatus => "Status das câmeras",
        CommandIntent.ListCaptures => "Últimas capturas",
        CommandIntent.Unsupported => "Não implementado",
        CommandIntent.Confirm => "Confirmação",
        CommandIntent.Decline => "Recusa",
        _ => "Não entendido",
    };

    public static string IntentLabel(string? intentName) =>
        Enum.TryParse<CommandIntent>(intentName, out var intent) ? IntentLabel(intent) : "—";

    public static string SourceLabel(string? source) => source switch
    {
        "rules" => "Regras",
        "llm" => "IA",
        "offer" => "Oferta aceita",
        "error" => "Erro na IA",
        _ => "—",
    };

    private static string Stamp(DateTime at) =>
        at.Date == DateTime.Today ? at.ToString("HH:mm") : at.ToString("dd/MM HH:mm");
}
