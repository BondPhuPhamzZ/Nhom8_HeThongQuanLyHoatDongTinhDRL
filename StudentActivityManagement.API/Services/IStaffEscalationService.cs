using StudentActivityManagement.API.DTOs.ClassSpace;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services;

public interface IStaffEscalationService
{
    Task<ApiResponseDto<PagedResultDto<ThreadEscalationDto>>> GetInboxAsync(string? status, int pageIndex, int pageSize);

    Task<ApiResponseDto<ThreadEscalationDto>> GetAsync(int escalationId);

    Task<ApiResponseDto<ThreadEscalationDto>> AnswerAsync(int staffUserId, int escalationId, StaffEscalationAnswerDto request);
}
