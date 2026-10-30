using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.ClassSpace;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>
/// US-09: the signed-in student's own class discussion space. The class is taken from the account, so the
/// routes carry no class id and nobody can read another class's space.
/// </summary>
[ApiController]
[Route("api/v1/class-space")]
[Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public sealed class ClassSpaceController : ApiControllerBase
{
    private readonly IClassSpaceService _classSpaceService;

    public ClassSpaceController(IClassSpaceService classSpaceService)
    {
        _classSpaceService = classSpaceService;
    }

    /// <summary>Chủ đề của lớp mình: ghim trước, sau đó mới hoạt động gần nhất; lọc theo loại và từ khóa.</summary>
    [HttpGet("threads")]
    [ProducesResponseType(typeof(ApiResponseDto<ClassSpaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetThreads([FromQuery] string? kind, [FromQuery] string? search, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.GetThreadsAsync(userId, kind, search, pageIndex, pageSize);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Chi tiết chủ đề kèm các trả lời theo thứ tự thời gian.</summary>
    [HttpGet("threads/{threadId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<ClassThreadDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetThread(int threadId)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.GetThreadAsync(userId, threadId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Đặt câu hỏi (Question) hoặc mở thảo luận (Discussion) trong lớp mình.</summary>
    [HttpPost("threads")]
    [ProducesResponseType(typeof(ApiResponseDto<ClassThreadDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateThread([FromBody] ClassThreadCreateDto request)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.CreateThreadAsync(userId, request);
        return result.Success ? Created($"/api/v1/class-space/threads/{result.Data!.Id}", result) : ProblemFrom(result);
    }

    /// <summary>Trả lời một chủ đề chưa đóng.</summary>
    [HttpPost("threads/{threadId:int}/replies")]
    [ProducesResponseType(typeof(ApiResponseDto<ClassThreadReplyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reply(int threadId, [FromBody] ClassThreadReplyCreateDto request)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.ReplyAsync(userId, threadId, request);
        return result.Success ? Created($"/api/v1/class-space/threads/{threadId}", result) : ProblemFrom(result);
    }

    /// <summary>Người tạo xóa chủ đề khi chưa có ai khác trả lời.</summary>
    [HttpDelete("threads/{threadId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteThread(int threadId)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.DeleteThreadAsync(userId, threadId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Người viết xóa trả lời của mình (trừ trả lời đã được chọn làm câu trả lời).</summary>
    [HttpDelete("threads/{threadId:int}/replies/{replyId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReply(int threadId, int replyId)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.DeleteReplyAsync(userId, threadId, replyId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Lớp trưởng chuyển câu hỏi cần xác nhận của chủ đề lên Phòng QL HSSV (US-15).</summary>
    [HttpPost("threads/{threadId:int}/escalations")]
    [ProducesResponseType(typeof(ApiResponseDto<ThreadEscalationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Escalate(int threadId, [FromBody] ThreadEscalationCreateDto request)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.EscalateAsync(userId, threadId, request);
        return result.Success ? Created($"/api/v1/class-space/escalations/{result.Data!.Id}", result) : ProblemFrom(result);
    }

    /// <summary>Các câu hỏi lớp trưởng đã chuyển lên, mới nhất trước; lọc theo trạng thái (US-15).</summary>
    [HttpGet("escalations")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<ThreadEscalationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEscalations([FromQuery] string? status, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.GetEscalationsAsync(userId, status, pageIndex, pageSize);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Lớp trưởng rút lại câu hỏi khi Phòng QL HSSV chưa trả lời (US-15).</summary>
    [HttpPost("escalations/{escalationId:int}/withdraw")]
    [ProducesResponseType(typeof(ApiResponseDto<ThreadEscalationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WithdrawEscalation(int escalationId)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await _classSpaceService.WithdrawEscalationAsync(userId, escalationId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

    private ObjectResult InvalidSession() => ApiProblem(
        StatusCodes.Status401Unauthorized,
        ApiErrorCodes.AuthenticationRequired,
        "Không xác định được sinh viên.");
}
