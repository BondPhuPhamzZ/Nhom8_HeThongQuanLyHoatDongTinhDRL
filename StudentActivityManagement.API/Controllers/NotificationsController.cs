using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Notifications;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-06: in-app reminders belonging to the signed-in student.</summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<UserNotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] bool unreadOnly = false, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        if (!TryUserId(out var userId)) return InvalidSession();
        var result = await _service.GetMineAsync(userId, unreadOnly, pageIndex, pageSize);
        return Ok(result);
    }

    [HttpPost("{id:int}/read")]
    [ProducesResponseType(typeof(ApiResponseDto<UserNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id)
    {
        if (!TryUserId(out var userId)) return InvalidSession();
        var result = await _service.MarkReadAsync(userId, id);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    private bool TryUserId(out int userId) => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

    private ObjectResult InvalidSession() => ApiProblem(StatusCodes.Status401Unauthorized, ApiErrorCodes.AuthenticationRequired, "Không xác định được sinh viên.");
}
