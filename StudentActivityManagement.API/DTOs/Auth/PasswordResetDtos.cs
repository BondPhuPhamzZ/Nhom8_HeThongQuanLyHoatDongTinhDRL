using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Auth;

public sealed class ForgotPasswordDto
{
    [Required, StringLength(150)]
    public string Login { get; set; } = string.Empty;
}

public sealed class ResetPasswordDto
{
    [Required, StringLength(150)]
    public string Login { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record TemporaryPasswordDto(string TemporaryPassword);
