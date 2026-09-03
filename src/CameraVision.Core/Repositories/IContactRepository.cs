using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface IContactRepository
{
    /// <summary>Contacts of one tenant, or every tenant's when <paramref name="tenantId"/> is null.</summary>
    Task<IReadOnlyList<Contact>> GetAllAsync(int? tenantId = null, CancellationToken ct = default);

    Task<Contact?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(int tenantId, string name, int? exceptId = null, CancellationToken ct = default);
    Task AddAsync(Contact contact, CancellationToken ct = default);
    Task UpdateAsync(Contact contact, CancellationToken ct = default);

    /// <summary>How many rule notifications (triggers) reference the contact.</summary>
    Task<int> CountTriggerUsagesAsync(int contactId, CancellationToken ct = default);

    /// <summary>Deletes the contact and removes it from every trigger that referenced it.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Contacts (any tenant) whose normalized WhatsApp number is one of <paramref name="candidates"/>.</summary>
    Task<IReadOnlyList<Contact>> FindByWhatsAppNumberAsync(IReadOnlyCollection<string> candidates, CancellationToken ct = default);

    /// <summary>Addresses of the tenant's contacts flagged for camera-health alerts on that channel.</summary>
    Task<IReadOnlyList<string>> GetHealthRecipientsAsync(int tenantId, AlertChannel channel, CancellationToken ct = default);
}
