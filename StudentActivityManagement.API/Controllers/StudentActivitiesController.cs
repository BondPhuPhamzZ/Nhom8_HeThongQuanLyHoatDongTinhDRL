using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;
using System.Security.Claims;

namespace StudentActivityManagement.API.Controllers
{
    [ApiController]
    [Route("api/v1/student-activities")]
    [Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
    public class StudentActivitiesController : ApiControllerBase
    {
        private readonly IActivityService _activityService;

        public StudentActivitiesController(IActivityService activityService)
        {
            _activityService = activityService;
        }

        [HttpGet]
        public async Task<IActionResult> GetActivities([FromQuery] string? campus, [FromQuery] string? search, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            if (!TryGetStudentId(out var studentId))
            {
                return InvalidSession();
            }

            var result = await _activityService.GetStudentActivitiesAsync(studentId, campus, search, pageIndex, pageSize);
            if (!result.Success) return ProblemFrom(result);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetActivityById(int id)
        {
            if (!TryGetStudentId(out var studentId))
            {
                return InvalidSession();
            }

            var result = await _activityService.GetStudentActivityByIdAsync(id, studentId);
            if (!result.Success) return ProblemFrom(result);
            return Ok(result);
        }

        [HttpPost("{id}/register")]
        public async Task<IActionResult> RegisterActivity(int id)
        {
            if (!TryGetStudentId(out var studentId))
            {
                return InvalidSession();
            }

            var result = await _activityService.RegisterActivityAsync(id, studentId);
            if (!result.Success) return ProblemFrom(result);
            return Ok(result);
        }

        [HttpDelete("{activityId}/registration")]
        [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelRegistration(int activityId)
        {
            if (!TryGetStudentId(out var studentId))
            {
                return InvalidSession();
            }

            var result = await _activityService.CancelRegistrationAsync(activityId, studentId);
            if (!result.Success) return ProblemFrom(result);
            return Ok(result);
        }

        private bool TryGetStudentId(out int studentId)
        {
            var studentIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(studentIdValue, out studentId);
        }

        private ObjectResult InvalidSession() => ApiProblem(
            StatusCodes.Status401Unauthorized,
            ApiErrorCodes.AuthenticationRequired,
            "Không xác định được sinh viên.");
    }
}

