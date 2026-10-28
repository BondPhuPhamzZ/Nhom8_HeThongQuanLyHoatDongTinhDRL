using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.CheckIn;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-07: Phòng QL HSSV issues and rotates the check-in code of each session.</summary>
[ApiController]
[Route("api/v1/admin/activities/{activityId:int}")]
[Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class ActivityCheckInCodesController : ApiControllerBase
{
    private readonly ICheckInService _checkInService;

    public ActivityCheckInCodesController(ICheckInService checkInService)
    {
        _checkInService = checkInService;
    }

    /// <summary>Mã check-in, cửa sổ check-in và số lượt đã check-in của từng buổi.</summary>
    [HttpGet("check-in-codes")]
    [ProducesResponseType(typeof(ApiResponseDto<ActivityCheckInCodesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCodes(int activityId)
    {
        var result = await _checkInService.GetSessionCodesAsync(activityId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>
    /// Tạo (hoặc đổi) mã check-in 6 ký tự cho một buổi. Mã cũ hết hiệu lực ngay.
    /// Chỉ cho hoạt động Published/Ongoing dùng phương thức Code.
    /// </summary>
    [HttpPost("sessions/{sessionId:int}/check-in-code")]
    [ProducesResponseType(typeof(ApiResponseDto<SessionCheckInCodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerateCode(int activityId, int sessionId)
    {
        var result = await _checkInService.GenerateSessionCodeAsync(activityId, sessionId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
