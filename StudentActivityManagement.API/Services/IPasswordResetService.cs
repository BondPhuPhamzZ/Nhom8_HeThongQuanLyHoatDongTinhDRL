using StudentActivityManagement.API.DTOs.Auth;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services;

public interface IPasswordResetService
{
    Task<ApiResponseDto<object>> RequestCodeAsync(ForgotPasswordDto request, string? ip, CancellationToken ct);
    Task<ApiResponseDto<object>> ResetAsync(ResetPasswordDto request, CancellationToken ct);
    Task<ApiResponseDto<TemporaryPasswordDto>> IssueTemporaryAsync(int userId, CancellationToken ct);
}
