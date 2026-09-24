using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;
using System.Security.Claims;

namespace StudentActivityManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Role.Student)]
    public class StudentActivitiesController : ControllerBase
    {
        private readonly IActivityService _activityService;

        public StudentActivitiesController(IActivityService activityService)
        {
            _activityService = activityService;
        }

        [HttpGet]
        public async Task<IActionResult> GetActivities([FromQuery] string? campus, [FromQuery] string? search, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _activityService.GetActivitiesAsync(campus, "Published", search, pageIndex, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetActivityById(int id)
        {
            var result = await _activityService.GetActivityByIdAsync(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpPost("{id}/register")]
        public async Task<IActionResult> RegisterActivity(int id)
        {
            var studentIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(studentIdStr) || !int.TryParse(studentIdStr, out int studentId))
            {
                return Unauthorized(ApiResponseDto<bool>.Fail("Không xác định được sinh viên."));
            }

            var result = await _activityService.RegisterActivityAsync(id, studentId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}

