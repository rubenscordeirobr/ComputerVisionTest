using CameraVision.Core.Commands;

namespace CameraVision.Web.Services;

/// <summary>PT-BR replies of the WhatsApp command agent, in one place.</summary>
public static class CommandReplyText
{
    public const string AskUntilWhen = "Até quando? (ex.: \"2 horas\", \"até as 22h\", \"até amanhã\", \"até eu desativar\")";

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

    public static string Unknown() =>
        "Não entendi. Envie \"ativar alertas\" para receber as capturas no WhatsApp ou \"desativar alertas\" para encerrar.";

    public const string ChannelOffNote =
        "Obs.: o canal WhatsApp está desativado nas configurações de alertas do seu cliente — as mensagens só saem quando ele for ligado.";

    /// <summary>Intent label for the system pages.</summary>
    public static string IntentLabel(CommandIntent intent) => intent switch
    {
        CommandIntent.EnableAlerts => "Ativar alertas",
        CommandIntent.DisableAlerts => "Desativar alertas",
        CommandIntent.SetDuration => "Definir prazo",
        _ => "Não entendido",
    };

    public static string IntentLabel(string? intentName) =>
        Enum.TryParse<CommandIntent>(intentName, out var intent) ? IntentLabel(intent) : "—";

    public static string SourceLabel(string? source) => source switch
    {
        "rules" => "Regras",
        "llm" => "IA",
        "error" => "Erro na IA",
        _ => "—",
    };

    private static string Stamp(DateTime at) =>
        at.Date == DateTime.Today ? at.ToString("HH:mm") : at.ToString("dd/MM HH:mm");
}
