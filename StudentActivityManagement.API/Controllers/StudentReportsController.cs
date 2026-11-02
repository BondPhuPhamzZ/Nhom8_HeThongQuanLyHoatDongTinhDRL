using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Student;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

[ApiController]
[Route("api/v1/student/reports")]
[Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class StudentReportsController : ApiControllerBase
{
    private readonly IStudentReportService _reports;

    public StudentReportsController(IStudentReportService reports) => _reports = reports;

    [HttpGet("evaluation-periods")]
    [ProducesResponseType(typeof(ApiResponseDto<List<StudentEvaluationPeriodDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriods()
    {
        if (!TryGetStudentId(out var studentId)) return InvalidSession();
        return Ok(await _reports.GetPeriodsAsync(studentId));
    }

    [HttpGet("activity-history")]
    [ProducesResponseType(typeof(ApiResponseDto<StudentEvaluationHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActivityHistory([FromQuery] int? periodId, [FromQuery] string? registrationStatus)
    {
        if (!TryGetStudentId(out var studentId)) return InvalidSession();
        var result = await _reports.GetActivityHistoryAsync(studentId, periodId, registrationStatus);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    private bool TryGetStudentId(out int studentId) =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out studentId);

    private ObjectResult InvalidSession() => ApiProblem(
        StatusCodes.Status401Unauthorized,
        ApiErrorCodes.AuthenticationRequired,
        "Không xác định được sinh viên.");
}
