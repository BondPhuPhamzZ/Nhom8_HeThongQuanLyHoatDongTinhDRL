using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-26: check-in behavior and reminder offsets for one activity.</summary>
[ApiController]
[Route("api/v1/admin/activities/{activityId:int}")]
[Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class ActivitySettingsController : ApiControllerBase
{
    private readonly IActivitySettingsService _settings;

    public ActivitySettingsController(IActivitySettingsService settings) => _settings = settings;

    [HttpGet("check-in-config")]
    [ProducesResponseType(typeof(ApiResponseDto<CheckInConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCheckInConfig(int activityId)
    {
        var result = await _settings.GetCheckInConfigAsync(activityId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    [HttpPut("check-in-config")]
    [ProducesResponseType(typeof(ApiResponseDto<CheckInConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCheckInConfig(int activityId, [FromBody] CheckInConfigUpdateDto request)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return ApiProblem(StatusCodes.Status401Unauthorized, ApiErrorCodes.AuthenticationRequired, "Phiên đăng nhập không hợp lệ.");

        var result = await _settings.UpdateCheckInConfigAsync(activityId, request, userId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    [HttpGet("reminder-schedule")]
    [ProducesResponseType(typeof(ApiResponseDto<ReminderScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReminderSchedule(int activityId)
    {
        var result = await _settings.GetReminderScheduleAsync(activityId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    [HttpPut("reminder-schedule")]
    [ProducesResponseType(typeof(ApiResponseDto<ReminderScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReminderSchedule(int activityId, [FromBody] ReminderScheduleUpdateDto request)
    {
        var result = await _settings.UpdateReminderScheduleAsync(activityId, request);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
