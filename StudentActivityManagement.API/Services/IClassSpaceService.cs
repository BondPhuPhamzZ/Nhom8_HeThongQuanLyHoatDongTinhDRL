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
}
