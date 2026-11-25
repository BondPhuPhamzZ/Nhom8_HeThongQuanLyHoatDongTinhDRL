using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.Web.Api;

namespace StudentActivityManagement.Web.Models;

public sealed class ForgotPasswordViewModel
{
    [Required, StringLength(150)]
    [Display(Name = "Mã tài khoản hoặc email")]
    public string Login { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required, StringLength(150)]
    [Display(Name = "Mã tài khoản hoặc email")]
    public string Login { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Mã xác nhận")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed record AccountDetailsViewModel(AccountDto Account, string? TemporaryPassword = null);

public sealed class ProfilePageViewModel
{
    public PersonalProfile? Details { get; set; }

    [Required(ErrorMessage = "Nhập email.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập số điện thoại.")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu hiện tại.")]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại.")]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập lại mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class AccountListViewModel
{
    public PagedResult<AccountDto> Page { get; set; } = new();
    public string? Search { get; set; }
    public string? Role { get; set; }
    /// <summary>"true", "false" or empty for all.</summary>
    public string? Active { get; set; }
    public int CurrentUserId { get; set; }
}

public sealed class AccountCreateViewModel
{
    [Required(ErrorMessage = "Nhập mã cán bộ."), StringLength(50)]
    [Display(Name = "Mã đăng nhập")]
    public string StudentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập họ và tên."), StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập email."), EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu."), MinLength(8, ErrorMessage = "Mật khẩu từ 8 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Required]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = "HSSV";

    [Display(Name = "Cơ sở")]
    public string Campus { get; set; } = "Cơ sở 1";

    public AccountCreateRequest ToRequest() => new()
    {
        StudentCode = StudentCode.Trim(),
        FullName = FullName.Trim(),
        Email = Email.Trim(),
        Password = Password,
        Phone = Phone?.Trim() ?? string.Empty,
        Role = Role,
        Campus = Campus
    };
}
