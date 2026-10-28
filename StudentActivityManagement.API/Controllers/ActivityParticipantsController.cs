using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Participants;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-21: Phòng QL HSSV checks registrations, attendance and evidence of an activity.</summary>
[ApiController]
[Route("api/v1/admin/activities/{activityId:int}")]
[Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class ActivityParticipantsController : ApiControllerBase
{
    private readonly IParticipantService _participantService;

    public ActivityParticipantsController(IParticipantService participantService)
    {
        _participantService = participantService;
    }

    /// <summary>
    /// Danh sách người đăng ký kèm check-in từng buổi và số minh chứng theo trạng thái duyệt.
    /// Lọc theo trạng thái đăng ký (Registered, Attended, Cancelled), mã/tên sinh viên, hoặc còn minh chứng chờ duyệt.
    /// </summary>
    [HttpGet("participants")]
    [ProducesResponseType(typeof(ApiResponseDto<ActivityParticipantsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParticipants(
        int activityId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool? hasPendingEvidence,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _participantService.GetParticipantsAsync(activityId, status, search, hasPendingEvidence, pageIndex, pageSize);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Chi tiết một người đăng ký: check-in từng buổi và toàn bộ minh chứng.</summary>
    [HttpGet("participants/{registrationId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<ParticipantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParticipant(int activityId, int registrationId)
    {
        var result = await _participantService.GetParticipantAsync(activityId, registrationId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>
    /// Điểm danh thủ công cho một buổi (phương thức Manual, ghi người thực hiện). Dùng được với cả hoạt động
    /// check-in bằng mã và ngoài cửa sổ check-in; lần đầu chuyển đăng ký sang Attended.
    /// </summary>
    [HttpPost("sessions/{sessionId:int}/check-ins")]
    [ProducesResponseType(typeof(ApiResponseDto<ParticipantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordCheckIn(int activityId, int sessionId, [FromBody] ManualCheckInRequestDto request)
    {
        if (!TryGetUserId(out var staffId))
        {
            return InvalidSession();
        }

        var result = await _participantService.RecordManualCheckInAsync(activityId, sessionId, request.RegistrationId, staffId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Hủy ghi nhận tham gia một buổi (điểm danh nhầm). Không còn buổi nào thì đăng ký trở về Registered.</summary>
    [HttpDelete("sessions/{sessionId:int}/check-ins/{registrationId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<ParticipantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveCheckIn(int activityId, int sessionId, int registrationId)
    {
        var result = await _participantService.RemoveCheckInAsync(activityId, sessionId, registrationId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Duyệt minh chứng: Accepted, Rejected (bắt buộc ghi lý do) hoặc Pending để mở lại.</summary>
    [HttpPatch("evidences/{evidenceId:int}/review")]
    [ProducesResponseType(typeof(ApiResponseDto<StaffEvidenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewEvidence(int activityId, int evidenceId, [FromBody] EvidenceReviewRequestDto request)
    {
        if (!TryGetUserId(out var staffId))
        {
            return InvalidSession();
        }

        var result = await _participantService.ReviewEvidenceAsync(activityId, evidenceId, request.Status, request.Note, staffId);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Tải file minh chứng của sinh viên.</summary>
    [HttpGet("evidences/{evidenceId:int}/file")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadEvidence(int activityId, int evidenceId)
    {
        var result = await _participantService.OpenEvidenceFileAsync(activityId, evidenceId);
        if (!result.Success || result.Data == null)
        {
            return ProblemFrom(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

    private ObjectResult InvalidSession() => ApiProblem(
        StatusCodes.Status401Unauthorized,
        ApiErrorCodes.AuthenticationRequired,
        "Không xác định được người dùng.");
}
