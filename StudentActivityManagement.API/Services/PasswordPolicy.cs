using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public static class PasswordPolicy
{
    public static string? Validate(string password, User user)
    {
        if (password.Length is < 8 or > 64) return "Mật khẩu mới phải dài từ 8 đến 64 ký tự.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Mật khẩu mới phải có ít nhất một chữ cái và một chữ số.";
        if (user.StudentCode.Length != 0 && password.Contains(user.StudentCode, StringComparison.OrdinalIgnoreCase))
            return "Mật khẩu mới không được chứa mã tài khoản.";
        if (PasswordHasher.VerifyPassword(password, user.PasswordHash))
            return "Mật khẩu mới phải khác mật khẩu hiện tại.";
        return null;
    }
}
