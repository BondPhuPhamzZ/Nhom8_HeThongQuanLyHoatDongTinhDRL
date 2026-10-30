using StudentActivityManagement.API.DTOs.ClassSpace;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services;

/// <summary>
/// US-09: a class's private discussion space. Every method takes the signed-in user and works only inside
/// that user's own class; threads of other classes behave as if they did not exist (404).
/// </summary>
public interface IClassSpaceService
{
    Task<ApiResponseDto<ClassSpaceDto>> GetThreadsAsync(int userId, string? kind, string? search, int pageIndex, int pageSize);

    Task<ApiResponseDto<ClassThreadDetailDto>> GetThreadAsync(int userId, int threadId);

    Task<ApiResponseDto<ClassThreadDetailDto>> CreateThreadAsync(int userId, ClassThreadCreateDto request);

    Task<ApiResponseDto<ClassThreadReplyDto>> ReplyAsync(int userId, int threadId, ClassThreadReplyCreateDto request);

    Task<ApiResponseDto<bool>> DeleteThreadAsync(int userId, int threadId);

    Task<ApiResponseDto<bool>> DeleteReplyAsync(int userId, int threadId, int replyId);

    /// <summary>US-15: the class monitor raises a thread to Phòng QL HSSV (one Pending escalation per thread).</summary>
    Task<ApiResponseDto<ThreadEscalationDto>> EscalateAsync(int userId, int threadId, ThreadEscalationCreateDto request);

    /// <summary>US-15: the monitor's view of the class's escalations, newest first.</summary>
    Task<ApiResponseDto<PagedResultDto<ThreadEscalationDto>>> GetEscalationsAsync(int userId, string? status, int pageIndex, int pageSize);

    /// <summary>US-15: the monitor withdraws an escalation that is still Pending.</summary>
    Task<ApiResponseDto<ThreadEscalationDto>> WithdrawEscalationAsync(int userId, int escalationId);
}
