using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.Web.Api;

namespace StudentActivityManagement.Web.Models;

public sealed class ClassListViewModel
{
    public List<ClassDto> Classes { get; set; } = [];
    public ClassCreateForm NewClass { get; set; } = new();
}

public sealed class ClassCreateForm
{
    [Required(ErrorMessage = "Nhập mã lớp."), StringLength(20)]
    [Display(Name = "Mã lớp")]
    public string ClassCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập tên lớp."), StringLength(100)]
    [Display(Name = "Tên lớp")]
    public string ClassName { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Khoa")]
    public string? Department { get; set; }
}

public sealed class ClassDetailViewModel
{
    public ClassDto Class { get; set; } = new();
    public List<StudentDto> Students { get; set; } = [];
}
