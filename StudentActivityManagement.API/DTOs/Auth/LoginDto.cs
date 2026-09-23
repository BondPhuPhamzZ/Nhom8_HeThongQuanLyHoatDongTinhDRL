using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Auth
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Mã sinh viên hoặc Email không được để trống")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        public string Password { get; set; } = string.Empty;
    }
}
