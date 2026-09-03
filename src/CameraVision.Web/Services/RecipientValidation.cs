using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;

namespace CameraVision.Web.Services;

/// <summary>PT-BR field validators for e-mail addresses and WhatsApp numbers (over RecipientNormalizer).</summary>
public static class RecipientValidation
{
    public const string PhoneLabel = "WhatsApp (ex.: +5549999999999)";
    public const string InvalidEmail = "E-mail inválido.";
    public const string InvalidPhone = "Número inválido. Use o formato +5549999999999.";

    /// <summary>Null when valid; an empty value is valid (use Required for mandatory fields).</summary>
    public static string? OptionalEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) || RecipientNormalizer.NormalizeEmail(value) != null
            ? null
            : InvalidEmail;

    public static string? OptionalPhone(string? value) =>
        string.IsNullOrWhiteSpace(value) || RecipientNormalizer.NormalizePhone(value) != null
            ? null
            : InvalidPhone;

    public static string? Optional(AlertChannel channel, string? value) =>
        channel == AlertChannel.Email ? OptionalEmail(value) : OptionalPhone(value);

    public static string DuplicateError(AlertChannel channel) =>
        channel == AlertChannel.Email ? "E-mail já adicionado." : "Número já adicionado.";
}
