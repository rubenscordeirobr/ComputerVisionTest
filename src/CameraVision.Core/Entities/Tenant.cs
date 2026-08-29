namespace CameraVision.Core.Entities;

/// <summary>
/// One tenant ("Empresa" in the UI). Owns cameras, capture rules, captures,
/// health events, alert recipients and users. Tenants are never deleted —
/// deactivating one blocks its users from signing in while keeping the data.
/// </summary>
public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>Tenant row + aggregate counts for the SuperAdmin listing.</summary>
public sealed record TenantSummary(Tenant Tenant, int UserCount, int CameraCount);
