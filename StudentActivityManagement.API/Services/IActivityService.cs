using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services
{
    public interface IActivityService
    {
        Task<ApiResponseDto<PagedResultDto<ActivityResponseDto>>> GetActivitiesAsync(
            string? campus, string? status, string? search, int pageIndex = 1, int pageSize = 10);

        Task<ApiResponseDto<ActivityResponseDto>> GetActivityByIdAsync(int id);
        Task<ApiResponseDto<ActivityResponseDto>> CreateActivityAsync(ActivityCreateDto request);
        Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityAsync(int id, ActivityUpdateDto request);
        Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityStatusAsync(int id, string newStatus);
        Task<ApiResponseDto<bool>> DeleteActivityAsync(int id);
        Task<ApiResponseDto<bool>> RegisterActivityAsync(int activityId, int studentId);
    }
}

