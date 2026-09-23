using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Auth
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Mã sinh viên không được để trống")]
        public string StudentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
        public string Password { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;
        public string Campus { get; set; } = "Cơ sở 1";
        public string AcademicYear { get; set; } = "K21";
        public int? ClassId { get; set; }
    }
}
