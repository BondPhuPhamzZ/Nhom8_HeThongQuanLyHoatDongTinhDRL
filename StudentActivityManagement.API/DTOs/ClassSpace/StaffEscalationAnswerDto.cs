using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.ClassSpace;

public sealed class StaffEscalationAnswerDto
{
    [Required(ErrorMessage = "Nhập nội dung phản hồi.")]
    [StringLength(4000, MinimumLength = 5, ErrorMessage = "Phản hồi từ 5 đến 4000 ký tự.")]
    public string Answer { get; set; } = string.Empty;
}
