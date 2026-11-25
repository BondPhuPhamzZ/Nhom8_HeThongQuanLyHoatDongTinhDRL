using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.Models;

public sealed class PasswordResetCode
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    [StringLength(64)]
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? UsedAt { get; set; }

    [StringLength(45)]
    public string? RequestedIp { get; set; }
}
