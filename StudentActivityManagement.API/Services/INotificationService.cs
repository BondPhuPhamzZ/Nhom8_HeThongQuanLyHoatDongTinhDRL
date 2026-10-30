using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Notifications;

namespace StudentActivityManagement.API.Services;

public interface INotificationService
{
    Task<ApiResponseDto<PagedResultDto<UserNotificationDto>>> GetMineAsync(int userId, bool unreadOnly, int pageIndex, int pageSize);
    Task<ApiResponseDto<UserNotificationDto>> MarkReadAsync(int userId, int notificationId);
    Task<int> DispatchDueRemindersAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
