using StudentActivityManagement.API.DTOs.Auth;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services
{
    public interface IAuthService
    {
        Task<ApiResponseDto<AuthResponseDto>> LoginAsync(LoginDto request);
        Task<ApiResponseDto<UserDto>> GetCurrentUserAsync(int userId);

        /// <summary>US-44: revokes every token of the account (all browsers and devices).</summary>
        Task<ApiResponseDto<object>> LogoutAsync(int userId);
    }
}
