namespace StudentActivityManagement.API.DTOs.Account;

/// <summary>
/// Thông tin tài khoản trả về cho Quản trị viên hệ thống. Không bao giờ chứa PasswordHash.
/// </summary>
public sealed class AccountResponseDto
{
    public int Id { get; set; }

    /// <summary>Mã đăng nhập (mã sinh viên hoặc mã cán bộ như ADMIN001).</summary>
    public string StudentCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Campus { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public int? ClassId { get; set; }
    public string? ClassCode { get; set; }
    public bool IsClassMonitor { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
