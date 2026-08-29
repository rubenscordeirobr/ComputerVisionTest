using CameraVision.Core.Entities;

namespace CameraVision.Web.Services;

/// <summary>PT-BR labels for user roles (shared by the users grid and dialogs).</summary>
public static class RoleUi
{
    public static string Label(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "Superadmin",
        UserRole.Admin => "Administrador",
        _ => "Usuário",
    };
}
