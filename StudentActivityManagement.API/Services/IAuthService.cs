using StudentActivityManagement.API.DTOs.Auth;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services
{
    public interface IAuthService
    {
        Task<ApiResponseDto<AuthResponseDto>> LoginAsync(LoginDto request);
        Task<ApiResponseDto<UserDto>> RegisterAsync(RegisterDto request);
        Task<ApiResponseDto<UserDto>> GetCurrentUserAsync(int userId);
    }
}
