namespace CameraVision.Core.Entities;

/// <summary>
/// A named recipient of one tenant ("Contatos"): an e-mail address and/or a WhatsApp
/// number. Capture-rule notifications (AlertTrigger) pick contacts; camera-health
/// alerts go to the contacts flagged with NotifyCameraHealth.
/// </summary>
public class Contact
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }

    /// <summary>Stored normalized: "+" followed by 10–15 digits (see RecipientNormalizer).</summary>
    public string? WhatsAppNumber { get; set; }

    /// <summary>Receives the camera health alerts (offline/weak/recovery) of the tenant.</summary>
    public bool NotifyCameraHealth { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? AddressFor(AlertChannel channel) => channel switch
    {
        AlertChannel.Email => Email,
        AlertChannel.WhatsApp => WhatsAppNumber,
        _ => null,
    };

    public bool HasAddressFor(AlertChannel channel) => !string.IsNullOrWhiteSpace(AddressFor(channel));
}
