using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Profile;

public sealed record ProfileDto(string StudentCode, string FullName, string Role, string? ClassCode, string Campus,
    string AcademicYear, string Email, string Phone, DateTime? UpdatedAt);

public sealed class UpdateContactDto
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
