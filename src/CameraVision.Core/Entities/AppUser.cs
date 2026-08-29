namespace CameraVision.Core.Entities;

public enum UserRole
{
    /// <summary>Viewer inside a tenant.</summary>
    User,

    /// <summary>Tenant administrator: manages the tenant's data and users.</summary>
    Admin,

    /// <summary>Platform operator: manages tenants and system settings. No tenant.</summary>
    SuperAdmin,
}

public class AppUser
{
    public int Id { get; set; }

    /// <summary>Null only for system users (SuperAdmin).</summary>
    public int? TenantId { get; set; }

    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }

    /// <summary>Identity-format password hash (PBKDF2). Never store plaintext.</summary>
    public string PasswordHash { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsSuperAdmin => Role == UserRole.SuperAdmin;

    /// <summary>Admin of a tenant or SuperAdmin — anything that may manage users.</summary>
    public bool IsAdmin => Role is UserRole.Admin or UserRole.SuperAdmin;
}
