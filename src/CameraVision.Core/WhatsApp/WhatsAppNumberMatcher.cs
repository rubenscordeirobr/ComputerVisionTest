using CameraVision.Core.Alerts;

namespace CameraVision.Core.WhatsApp;

/// <summary>
/// Normalized numbers a WhatsApp JID may correspond to among the stored contacts.
/// Brazilian mobile JIDs frequently omit the ninth digit (55 DD 8xxxx-xxxx), while
/// contacts are typed with it, so both spellings are candidates.
/// </summary>
public static class WhatsAppNumberMatcher
{
    public static IReadOnlyList<string> Candidates(string jidDigits)
    {
        var normalized = RecipientNormalizer.NormalizePhone(jidDigits);
        if (normalized == null)
            return [];

        var digits = normalized[1..];
        var candidates = new List<string> { normalized };
        if (digits.StartsWith("55"))
        {
            if (digits.Length == 12)
                candidates.Add("+" + digits[..4] + "9" + digits[4..]);
            else if (digits.Length == 13 && digits[4] == '9')
                candidates.Add("+" + digits[..4] + digits[5..]);
        }
        return candidates;
    }
}
