using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services
{
    public interface IActivityService
    {
        Task<ApiResponseDto<PagedResultDto<ActivityResponseDto>>> GetActivitiesAsync(
            string? campus, string? status, string? search, int pageIndex = 1, int pageSize = 10, bool upcomingOnly = false);

        Task<ApiResponseDto<ActivityResponseDto>> GetActivityByIdAsync(int id);
        Task<ApiResponseDto<PagedResultDto<ActivityResponseDto>>> GetStudentActivitiesAsync(
            int studentId, string? campus, string? search, int pageIndex = 1, int pageSize = 10);
        Task<ApiResponseDto<ActivityResponseDto>> GetStudentActivityByIdAsync(int id, int studentId);
        Task<ApiResponseDto<ActivityResponseDto>> CreateActivityAsync(ActivityCreateDto request);
        Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityAsync(int id, ActivityUpdateDto request);
        Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityStatusAsync(int id, string newStatus);
        Task<ApiResponseDto<bool>> DeleteActivityAsync(int id);
        Task<ApiResponseDto<bool>> RegisterActivityAsync(int activityId, int studentId);
        Task<ApiResponseDto<bool>> CancelRegistrationAsync(int activityId, int studentId);
    }
}

