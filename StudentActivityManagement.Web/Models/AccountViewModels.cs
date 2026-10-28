using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.Web.Api;

namespace StudentActivityManagement.Web.Models;

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
