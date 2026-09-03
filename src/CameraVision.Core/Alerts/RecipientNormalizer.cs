using System.Net.Mail;
using CameraVision.Core.Entities;

namespace CameraVision.Core.Alerts;

/// <summary>
/// Canonical form of an address, so the same person is never notified twice and
/// contacts are stored consistently: e-mails are trimmed and lower-cased; WhatsApp
/// numbers keep their digits only and come back as "+" + 10–15 digits (what the
/// Evolution API accepts). Null means the value is unusable.
/// </summary>
public static class RecipientNormalizer
{
    public static string? Normalize(AlertChannel channel, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return channel switch
        {
            AlertChannel.Email => NormalizeEmail(raw),
            AlertChannel.WhatsApp => NormalizePhone(raw),
            _ => null,
        };
    }

    public static string? NormalizeEmail(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0 || value.Any(char.IsWhiteSpace))
            return null;
        if (!MailAddress.TryCreate(value, out var address))
            return null;
        return address.Address.ToLowerInvariant();
    }

    public static string? NormalizePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        return digits.Length is >= 10 and <= 15 ? "+" + digits : null;
    }
}
