using StudentActivityManagement.API.DTOs.CheckIn;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services;

/// <summary>US-07: session codes for staff and code-based check-in for students.</summary>
public interface ICheckInService
{
    Task<ApiResponseDto<StudentCheckInStatusDto>> GetStudentStatusAsync(int activityId, int studentId);

    Task<ApiResponseDto<CheckInResultDto>> CheckInAsync(int activityId, int sessionId, int studentId, string code);

    Task<ApiResponseDto<ActivityCheckInCodesDto>> GetSessionCodesAsync(int activityId);

    Task<ApiResponseDto<SessionCheckInCodeDto>> GenerateSessionCodeAsync(int activityId, int sessionId);
}
