using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CameraVision.Infrastructure.Repositories;

public class ContactRepository(IDbContextFactory<AppDbContext> factory) : IContactRepository
{
    public async Task<IReadOnlyList<Contact>> GetAllAsync(int? tenantId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Contacts.AsNoTracking();
        if (tenantId is { } tid)
            query = query.Where(c => c.TenantId == tid);
        return await query.OrderBy(c => c.Name).ToListAsync(ct);
    }

    public async Task<Contact?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<bool> NameExistsAsync(int tenantId, string name, int? exceptId = null, CancellationToken ct = default)
    {
        var lowered = name.Trim().ToLowerInvariant();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Contacts.AnyAsync(
            c => c.TenantId == tenantId && c.Name.ToLower() == lowered && (exceptId == null || c.Id != exceptId), ct);
    }

    public async Task AddAsync(Contact contact, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Contact contact, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Contacts.Update(contact);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CountTriggerUsagesAsync(int contactId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var triggers = await TenantTriggersAsync(db, contactId, tracked: false, ct);
        return triggers.Count(t => t.ContactIds.Contains(contactId));
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contact == null)
            return;

        // Contact ids live in a JSON column, so the triggers referencing this
        // contact are fixed up here, in the same transaction as the delete.
        foreach (var trigger in await TenantTriggersAsync(db, id, tracked: true, ct))
        {
            if (trigger.ContactIds.Contains(id))
                trigger.ContactIds = trigger.ContactIds.Where(c => c != id).ToList();
        }

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetHealthRecipientsAsync(int tenantId, AlertChannel channel, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var contacts = await db.Contacts.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.NotifyCameraHealth)
            .ToListAsync(ct);
        return contacts
            .Select(c => c.AddressFor(channel))
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Triggers of every rule of the contact's tenant — the only ones that can reference it.</summary>
    private static async Task<List<AlertTrigger>> TenantTriggersAsync(AppDbContext db, int contactId, bool tracked, CancellationToken ct)
    {
        var tenantId = await db.Contacts.AsNoTracking()
            .Where(c => c.Id == contactId)
            .Select(c => (int?)c.TenantId)
            .FirstOrDefaultAsync(ct);
        if (tenantId == null)
            return [];

        var query = db.AlertTriggers
            .Where(t => db.CaptureRules.Any(r => r.Id == t.CaptureRuleId && r.TenantId == tenantId));
        if (!tracked)
            query = query.AsNoTracking();
        return await query.ToListAsync(ct);
    }
}
