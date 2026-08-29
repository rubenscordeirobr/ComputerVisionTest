using System.Security.Claims;
using CameraVision.Core.Entities;

namespace CameraVision.Core.Auth;

/// <summary>
/// Custom claim names + helpers shared by the web app (pages/policies) and the
/// API (/media tenant guard). Cookies issued before SPEC-14 lack these claims;
/// such sessions read as tenant-less non-admins and must sign in again.
/// </summary>
public static class AppClaims
{
    public const string DisplayName = "display_name";
    public const string Role = "role";
    public const string TenantId = "tenant_id";

    public static UserRole GetRole(this ClaimsPrincipal user) =>
        Enum.TryParse<UserRole>(user.FindFirst(Role)?.Value, out var role) ? role : UserRole.User;

    public static bool IsSuperAdmin(this ClaimsPrincipal user) =>
        user.GetRole() == UserRole.SuperAdmin;

    public static int? GetTenantId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(TenantId)?.Value, out var id) ? id : null;

    /// <summary>
    /// Repository filter for the signed-in user: null (no filter) for SuperAdmin,
    /// the user's tenant otherwise. A tenant user without a tenant claim (stale
    /// cookie) gets -1, which matches nothing.
    /// </summary>
    public static int? GetTenantFilter(this ClaimsPrincipal user) =>
        user.IsSuperAdmin() ? null : user.GetTenantId() ?? -1;

    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
}
