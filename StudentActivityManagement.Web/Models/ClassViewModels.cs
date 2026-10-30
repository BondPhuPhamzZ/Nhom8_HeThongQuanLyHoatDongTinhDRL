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

/// <summary>US-09: the student's class space. Space is null when the student has no class yet.</summary>
public sealed class ClassSpaceViewModel
{
    public ClassSpace? Space { get; set; }
    public string? Kind { get; set; }
    public string? Search { get; set; }
    public ClassThreadForm NewThread { get; set; } = new();
}

public sealed class ClassThreadForm
{
    [Required(ErrorMessage = "Chọn loại bài.")]
    [Display(Name = "Loại bài")]
    public string Kind { get; set; } = "Question";

    [Required(ErrorMessage = "Nhập tiêu đề."), StringLength(200, MinimumLength = 3, ErrorMessage = "Tiêu đề từ 3 đến 200 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập nội dung."), StringLength(4000, ErrorMessage = "Nội dung tối đa 4000 ký tự.")]
    [Display(Name = "Nội dung")]
    public string Body { get; set; } = string.Empty;
}
