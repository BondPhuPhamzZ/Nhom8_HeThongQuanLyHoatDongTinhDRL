using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Student
{
    public class StudentCreateDto
    {
        [Required(ErrorMessage = "Mã sinh viên không được để trống")]
        public string StudentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        public string Password { get; set; } = "123456";

        public string Phone { get; set; } = string.Empty;

        public string Role { get; set; } = Models.Role.Student;

        public string Campus { get; set; } = "Cơ sở 1";

        public string AcademicYear { get; set; } = "K21";

        public int? ClassId { get; set; }

        public bool IsClassMonitor { get; set; } = false;
    }
}
