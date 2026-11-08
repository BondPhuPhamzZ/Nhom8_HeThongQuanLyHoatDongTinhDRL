using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

[ApiController]
[Route("api/v1")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class PointAppealsController : ApiControllerBase
{
    private readonly IPointAppealService _appeals;
    public PointAppealsController(IPointAppealService appeals) => _appeals = appeals;

    [Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
    [HttpPost("student/point-appeals")]
    public async Task<IActionResult> Create([FromBody] CreatePointAppealDto request)
    {
        if (!TryUserId(out var id)) return InvalidSession();
        var result = await _appeals.CreateAsync(id, request.PointRecordId, request.Reason);
        return result.Success ? StatusCode(StatusCodes.Status201Created, result) : ProblemFrom(result);
    }

    [Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
    [HttpGet("student/point-appeals")]
    public async Task<IActionResult> Mine()
    {
        if (!TryUserId(out var id)) return InvalidSession();
        return Ok(await _appeals.GetMineAsync(id));
    }

    [Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
    [HttpPost("student/point-appeals/{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id)
    {
        if (!TryUserId(out var studentId)) return InvalidSession();
        var result = await _appeals.WithdrawAsync(studentId, id);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    [Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
    [HttpGet("admin/point-appeals")]
    public async Task<IActionResult> Inbox(string? status, int? periodId, int pageIndex = 1, int pageSize = 20)
    {
        var result = await _appeals.GetInboxAsync(status, periodId, pageIndex, pageSize);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    [Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
    [HttpPost("admin/point-appeals/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolvePointAppealDto request)
    {
        if (!TryUserId(out var staffId)) return InvalidSession();
        var result = await _appeals.ResolveAsync(id, request.Accept, request.Response, staffId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    private bool TryUserId(out int id) => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out id);
    private ObjectResult InvalidSession() => ApiProblem(401, ApiErrorCodes.AuthenticationRequired, "Không xác định được người dùng.");
}
