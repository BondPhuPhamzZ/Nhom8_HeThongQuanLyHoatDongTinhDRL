using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Account;

/// <summary>
/// Tạo tài khoản cán bộ (HSSV hoặc SystemAdmin). Tài khoản sinh viên tạo qua /api/v1/admin/students.
/// </summary>
public sealed class AccountCreateDto
{
    [Required(ErrorMessage = "Mã đăng nhập không được để trống")]
    [StringLength(50, ErrorMessage = "Mã đăng nhập tối đa 50 ký tự")]
    public string StudentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [StringLength(100, ErrorMessage = "Họ và tên tối đa 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải từ 8 ký tự trở lên")]
    [StringLength(100, ErrorMessage = "Mật khẩu tối đa 100 ký tự")]
    public string Password { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Chỉ nhận HSSV hoặc SystemAdmin.</summary>
    [Required(ErrorMessage = "Vai trò không được để trống")]
    public string Role { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Cơ sở tối đa 50 ký tự")]
    public string Campus { get; set; } = "Cơ sở 1";
}

/// <summary>
/// Đổi vai trò giữa HSSV và SystemAdmin. Monitor chỉ đổi qua API chỉ định lớp trưởng.
/// </summary>
public sealed class AccountRoleUpdateDto
{
    [Required(ErrorMessage = "Vai trò không được để trống")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Khóa (false) hoặc mở khóa (true) tài khoản.
/// </summary>
public sealed class AccountActiveStatusUpdateDto
{
    [Required(ErrorMessage = "Trạng thái isActive không được để trống")]
    public bool? IsActive { get; set; }
}
