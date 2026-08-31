namespace CameraVision.Web.Services;

/// <summary>Shared PT-BR field validators for the system settings pages.</summary>
public static class SettingsValidation
{
    public static string? OptionalEmail(string value) =>
        string.IsNullOrWhiteSpace(value) || System.Net.Mail.MailAddress.TryCreate(value, out _)
            ? null
            : "E-mail inválido.";

    public static string? OptionalUrl(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            ? null
            : "URL inválida. Use http:// ou https://.";
}
