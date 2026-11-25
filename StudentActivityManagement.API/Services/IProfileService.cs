using StudentActivityManagement.API.DTOs.Auth;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Profile;

namespace StudentActivityManagement.API.Services;

public interface IProfileService
{
    Task<ApiResponseDto<ProfileDto>> GetAsync(int userId, CancellationToken ct);
    Task<ApiResponseDto<ProfileDto>> UpdateAsync(int userId, UpdateContactDto request, CancellationToken ct);
    Task<ApiResponseDto<AuthResponseDto>> ChangePasswordAsync(int userId, ChangePasswordDto request, CancellationToken ct);
}
