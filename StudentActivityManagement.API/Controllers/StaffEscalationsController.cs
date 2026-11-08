using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Assistant;
using StudentActivityManagement.API.DTOs.ClassSpace;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services;
using StudentActivityManagement.API.Services.Assistant;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-20: Phòng QL HSSV receives and answers questions raised by class monitors.</summary>
[ApiController]
[Route("api/v1/admin/class-escalations")]
[Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class StaffEscalationsController : ApiControllerBase
{
    private readonly IStaffEscalationService _service;
    private readonly IStaffAiService _staffAi;

    public StaffEscalationsController(IStaffEscalationService service, IStaffAiService staffAi)
    {
        _service = service;
        _staffAi = staffAi;
    }

    /// <summary>
    /// US-34: bản nháp phản hồi bằng AI cho câu hỏi đang chờ, dựa trên quy tắc của hệ thống. Không lưu và không gửi gì:
    /// cán bộ đọc, sửa rồi tự gửi qua endpoint answer.
    /// </summary>
    [HttpPost("{id:int}/ai-draft")]
    [EnableRateLimiting(AssistantService.RateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponseDto<EscalationDraftDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DraftAnswer(int id)
    {
        var result = await _staffAi.DraftEscalationAnswerAsync(id, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Inbox, newest first; optionally filter Pending, Answered or Withdrawn.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<ThreadEscalationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInbox([FromQuery] string? status, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetInboxAsync(status, pageIndex, pageSize);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>One question and its answer or withdrawal state.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<ThreadEscalationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(int id)
    {
        var result = await _service.GetAsync(id);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Answer a Pending question exactly once; a withdrawn question cannot be answered.</summary>
    [HttpPost("{id:int}/answer")]
    [ProducesResponseType(typeof(ApiResponseDto<ThreadEscalationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Answer(int id, [FromBody] StaffEscalationAnswerDto request)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var staffUserId))
            return ApiProblem(StatusCodes.Status401Unauthorized, ApiErrorCodes.AuthenticationRequired, "Không xác định được cán bộ.");
        var result = await _service.AnswerAsync(staffUserId, id, request);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
