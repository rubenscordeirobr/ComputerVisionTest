namespace CameraVision.Core.Entities;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }

    /// <summary>Identity-format password hash (PBKDF2). Never store plaintext.</summary>
    public string PasswordHash { get; set; } = "";

    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
