using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.CheckIn;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-07: students and class monitors check in to a session with the code announced by the organiser.</summary>
[ApiController]
[Route("api/v1/student-activities/{activityId:int}")]
[Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class StudentCheckInsController : ApiControllerBase
{
    private readonly ICheckInService _checkInService;

    public StudentCheckInsController(ICheckInService checkInService)
    {
        _checkInService = checkInService;
    }

    /// <summary>
    /// Trạng thái check-in của sinh viên đang đăng nhập: phương thức, cửa sổ check-in và thời điểm đã check-in của từng buổi.
    /// </summary>
    [HttpGet("check-in-status")]
    [ProducesResponseType(typeof(ApiResponseDto<StudentCheckInStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(int activityId)
    {
        if (!TryGetStudentId(out var studentId))
        {
            return InvalidSession();
        }

        var result = await _checkInService.GetStudentStatusAsync(activityId, studentId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>
    /// Check-in một buổi bằng mã do ban tổ chức công bố. Chỉ trong cửa sổ check-in của buổi, mỗi buổi một lần;
    /// lần check-in đầu tiên chuyển đăng ký sang Attended.
    /// </summary>
    [HttpPost("sessions/{sessionId:int}/check-in")]
    [ProducesResponseType(typeof(ApiResponseDto<CheckInResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(int activityId, int sessionId, [FromBody] StudentCheckInRequestDto request)
    {
        if (!TryGetStudentId(out var studentId))
        {
            return InvalidSession();
        }

        var result = await _checkInService.CheckInAsync(activityId, sessionId, studentId, request.Code);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    private bool TryGetStudentId(out int studentId) =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out studentId);

    private ObjectResult InvalidSession() => ApiProblem(
        StatusCodes.Status401Unauthorized,
        ApiErrorCodes.AuthenticationRequired,
        "Không xác định được sinh viên.");
}
