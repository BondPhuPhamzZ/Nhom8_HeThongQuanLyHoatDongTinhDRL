using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.Web.Api;

namespace StudentActivityManagement.Web.Models;

public sealed class StudentListViewModel
{
    public PagedResult<StudentDto> Page { get; set; } = new();
    public List<ClassDto> Classes { get; set; } = [];
    public string? Search { get; set; }
    public string? Campus { get; set; }
    public string? AcademicYear { get; set; }
    public int? ClassId { get; set; }
}

public sealed class StudentFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Nhập mã sinh viên."), StringLength(20)]
    [Display(Name = "Mã sinh viên")]
    public string StudentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập họ và tên."), StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập email."), EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Required when creating; left empty on edit to keep the current password.</summary>
    [MinLength(6, ErrorMessage = "Mật khẩu từ 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string? Password { get; set; }

    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Nhập cơ sở.")]
    [Display(Name = "Cơ sở")]
    public string Campus { get; set; } = "Cơ sở 1";

    [Required(ErrorMessage = "Nhập khóa, ví dụ K22.")]
    [Display(Name = "Khóa")]
    public string AcademicYear { get; set; } = "K22";

    [Display(Name = "Lớp")]
    public int? ClassId { get; set; }

    public bool IsClassMonitor { get; set; }
    public List<ClassDto> Classes { get; set; } = [];

    public static StudentFormViewModel From(StudentDto student) => new()
    {
        Id = student.Id,
        StudentCode = student.StudentCode,
        FullName = student.FullName,
        Email = student.Email,
        Phone = student.Phone,
        Campus = student.Campus,
        AcademicYear = student.AcademicYear,
        ClassId = student.ClassId,
        IsClassMonitor = student.IsClassMonitor
    };

    public StudentWriteRequest ToRequest() => new()
    {
        StudentCode = Id.HasValue ? null : StudentCode.Trim(),
        FullName = FullName.Trim(),
        Email = Email.Trim(),
        Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
        Phone = Phone?.Trim() ?? string.Empty,
        Campus = Campus.Trim(),
        AcademicYear = AcademicYear.Trim(),
        ClassId = ClassId
    };
}
