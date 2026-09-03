namespace CameraVision.Core.Entities;

public enum SmtpSecurity
{
    None,
    StartTls,
    SslTls,
}

/// <summary>LLM provider used by the WhatsApp command agent when the rule-based parser gives up.</summary>
public enum AiProvider
{
    None,
    Gemini,
    Claude,
    DeepSeek,
}

/// <summary>Singleton row (Id = 1). Secrets are stored in plaintext — known v1 limitation.</summary>
public class SystemSettings
{
    public int Id { get; set; } = 1;

    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string SmtpSenderEmail { get; set; } = "";
    public string SmtpSenderName { get; set; } = "";
    public SmtpSecurity SmtpSecurity { get; set; } = SmtpSecurity.StartTls;

    /// <summary>Base URL used to build absolute links in alert e-mails (e.g. http://192.168.3.2:5210).</summary>
    public string PublicBaseUrl { get; set; } = "";

    public string EvolutionBaseUrl { get; set; } = "";
    public string EvolutionApiKey { get; set; } = "";
    public string EvolutionInstanceName { get; set; } = "";

    // AI model used to interpret WhatsApp commands (SPEC-17). None = rules only.
    public AiProvider AiProvider { get; set; } = AiProvider.None;
    public string AiModel { get; set; } = "";
    public string AiApiKey { get; set; } = "";

    // WhatsApp command agent: inbound webhook (registered on the Evolution instance)
    // and the default validity of an "ativar alertas" without an explicit end.
    public bool WhatsAppBotEnabled { get; set; }
    public int WhatsAppBotDefaultHours { get; set; } = 8;
    public string WhatsAppWebhookUrl { get; set; } = "http://host.docker.internal:5220/api/whatsapp/webhook";
    public string WhatsAppWebhookSecret { get; set; } = "";
}
