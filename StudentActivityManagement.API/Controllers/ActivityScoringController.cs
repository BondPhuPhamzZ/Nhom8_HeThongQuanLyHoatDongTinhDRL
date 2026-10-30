using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-22: confirm results of a completed activity and record training points once, with history.</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class ActivityScoringController : ApiControllerBase
{
    private readonly IScoringService _scoringService;

    public ActivityScoringController(IScoringService scoringService)
    {
        _scoringService = scoringService;
    }

    /// <summary>
    /// Bảng ghi điểm của hoạt động: kỳ đánh giá tương ứng, ai đủ điều kiện (đã check-in hoặc có minh chứng
    /// được duyệt) và quyết định điểm đã có.
    /// </summary>
    [HttpGet("activities/{activityId:int}/scoring")]
    [ProducesResponseType(typeof(ApiResponseDto<ActivityScoringDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScoring(int activityId)
    {
        var result = await _scoringService.GetActivityScoringAsync(activityId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>
    /// Xác nhận kết quả và ghi điểm (Approved) cho các đăng ký đã chọn, hoặc mọi đăng ký đủ điều kiện khi không gửi danh sách.
    /// Mỗi đăng ký chỉ ghi một lần; hoạt động phải Completed và kỳ đánh giá đang Open.
    /// </summary>
    [HttpPost("activities/{activityId:int}/scoring/approve")]
    [ProducesResponseType(typeof(ApiResponseDto<ApprovePointsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(int activityId, [FromBody] ApprovePointsRequestDto request)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var staffId))
        {
            return ApiProblem(StatusCodes.Status401Unauthorized, ApiErrorCodes.AuthenticationRequired, "Không xác định được người dùng.");
        }

        var result = await _scoringService.ApproveAsync(activityId, request, staffId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Lịch sử phê duyệt của một bản ghi điểm.</summary>
    [HttpGet("point-records/{pointRecordId:int}/history")]
    [ProducesResponseType(typeof(ApiResponseDto<List<PointRecordHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(int pointRecordId)
    {
        var result = await _scoringService.GetHistoryAsync(pointRecordId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
