using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Assistant;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services.Assistant;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-31: training-point assistant for students and class monitors (Gemini with read-only tools).</summary>
[ApiController]
[Route("api/v1/assistant")]
[Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class AssistantController : ApiControllerBase
{
    private readonly IAssistantService _assistant;

    public AssistantController(IAssistantService assistant) => _assistant = assistant;

    /// <summary>Whether the assistant can answer (switched on and the server has a Gemini key).</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponseDto<AssistantStatusDto>), StatusCodes.Status200OK)]
    public IActionResult GetStatus() => Ok(ApiResponseDto<AssistantStatusDto>.Ok(_assistant.GetStatus()));

    /// <summary>Answers the last user message of the conversation from the signed-in student's own data.</summary>
    [HttpPost("chat")]
    [EnableRateLimiting(AssistantService.RateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponseDto<AssistantReplyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Chat([FromBody] AssistantChatRequestDto request)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var studentId))
        {
            return ApiProblem(StatusCodes.Status401Unauthorized, ApiErrorCodes.AuthenticationRequired, "Không xác định được sinh viên.");
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var result = await _assistant.ChatAsync(studentId, role, request, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
