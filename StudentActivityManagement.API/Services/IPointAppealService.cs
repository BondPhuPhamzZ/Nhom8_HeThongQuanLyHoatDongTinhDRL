using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;

namespace StudentActivityManagement.API.Services;

public interface IPointAppealService
{
    Task<ApiResponseDto<PointAppealDto>> CreateAsync(int studentId, int pointRecordId, string reason);
    Task<ApiResponseDto<List<PointAppealDto>>> GetMineAsync(int studentId);
    Task<ApiResponseDto<PointAppealDto>> WithdrawAsync(int studentId, int appealId);
    Task<ApiResponseDto<PagedResultDto<PointAppealDto>>> GetInboxAsync(string? status, int? periodId, int pageIndex, int pageSize);
    Task<ApiResponseDto<PointAppealDto>> ResolveAsync(int appealId, bool accept, string response, int staffId);
}
