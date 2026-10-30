using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;

namespace StudentActivityManagement.API.Services;

/// <summary>US-22: evaluation periods managed by Phòng QL HSSV.</summary>
public interface IEvaluationPeriodService
{
    Task<ApiResponseDto<List<EvaluationPeriodDto>>> GetPeriodsAsync();

    Task<ApiResponseDto<EvaluationPeriodDto>> CreatePeriodAsync(EvaluationPeriodWriteDto request);

    Task<ApiResponseDto<EvaluationPeriodDto>> UpdatePeriodAsync(int id, EvaluationPeriodWriteDto request);

    Task<ApiResponseDto<EvaluationPeriodDto>> SetStatusAsync(int id, string status);
}

/// <summary>US-22: confirm results of a completed activity and record points once, with history.</summary>
public interface IScoringService
{
    Task<ApiResponseDto<ActivityScoringDto>> GetActivityScoringAsync(int activityId);

    Task<ApiResponseDto<ApprovePointsResultDto>> ApproveAsync(int activityId, ApprovePointsRequestDto request, int staffId);

    Task<ApiResponseDto<List<PointRecordHistoryDto>>> GetHistoryAsync(int pointRecordId);
}
