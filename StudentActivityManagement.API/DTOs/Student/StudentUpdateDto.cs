using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Student
{
    public class StudentUpdateDto
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = string.Empty;

        public string? Password { get; set; } // Leave null/empty to keep current password

        public string Phone { get; set; } = string.Empty;

        public string Role { get; set; } = Models.Role.Student;

        public string Campus { get; set; } = string.Empty;

        public string AcademicYear { get; set; } = string.Empty;

        public int? ClassId { get; set; }

        public bool IsClassMonitor { get; set; }

        public int AccumulatedPoints { get; set; }
    }
}
