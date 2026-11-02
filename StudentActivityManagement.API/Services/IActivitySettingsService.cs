using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services;

public interface IActivitySettingsService
{
    Task<ApiResponseDto<CheckInConfigDto>> GetCheckInConfigAsync(int activityId);
    Task<ApiResponseDto<CheckInConfigDto>> UpdateCheckInConfigAsync(int activityId, CheckInConfigUpdateDto request, int updatedById);
    Task<ApiResponseDto<ReminderScheduleDto>> GetReminderScheduleAsync(int activityId);
    Task<ApiResponseDto<ReminderScheduleDto>> UpdateReminderScheduleAsync(int activityId, ReminderScheduleUpdateDto request);
}
