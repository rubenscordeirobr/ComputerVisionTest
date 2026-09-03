using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface ICaptureRuleRepository
{
    /// <summary>Rules (with their triggers) of one tenant, or every tenant's when <paramref name="tenantId"/> is null.</summary>
    Task<IReadOnlyList<CaptureRule>> GetAllAsync(int? tenantId = null, CancellationToken ct = default);

    /// <summary>Enabled rules (with their triggers) of one tenant; null = union of all tenants (worker endpoint).</summary>
    Task<IReadOnlyList<CaptureRule>> GetEnabledAsync(int? tenantId = null, CancellationToken ct = default);

    Task<CaptureRule?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);

    /// <summary>Inserts the rule together with the triggers attached to it.</summary>
    Task AddAsync(CaptureRule rule, CancellationToken ct = default);

    /// <summary>Updates the rule's own columns only; triggers are managed through <see cref="ReplaceTriggersAsync"/>.</summary>
    Task UpdateAsync(CaptureRule rule, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Makes <paramref name="triggers"/> the rule's notifications: adds new ones (Id 0), updates existing, deletes missing.</summary>
    Task ReplaceTriggersAsync(int ruleId, IReadOnlyList<AlertTrigger> triggers, CancellationToken ct = default);

    /// <summary>Inserts (Id 0) or updates one trigger directly.</summary>
    Task SaveTriggerAsync(AlertTrigger trigger, CancellationToken ct = default);

    Task DeleteTriggersAsync(IEnumerable<int> ids, CancellationToken ct = default);
}
