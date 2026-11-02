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
public sealed class StudentPointsController : ApiControllerBase
{
    private readonly IStudentPointsService _points;

    public StudentPointsController(IStudentPointsService points) => _points = points;

    [HttpGet("points-and-recommendations")]
    [ProducesResponseType(typeof(ApiResponseDto<StudentPointsRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPointsAndRecommendations([FromQuery] int? periodId)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var studentId))
        {
            return ApiProblem(StatusCodes.Status401Unauthorized, ApiErrorCodes.AuthenticationRequired, "Không xác định được sinh viên.");
        }

        var result = await _points.GetPointsAndRecommendationsAsync(studentId, periodId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
