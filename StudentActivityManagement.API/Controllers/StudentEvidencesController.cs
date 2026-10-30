using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Evidence;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

[ApiController]
[Route("api/v1/student-activities/{activityId:int}/evidences")]
[Authorize(Policy = AuthorizationPolicies.StudentOrMonitor)]
public class StudentEvidencesController : ApiControllerBase
{
    private readonly IEvidenceService _evidenceService;

    public StudentEvidencesController(IEvidenceService evidenceService)
    {
        _evidenceService = evidenceService;
    }

    /// <summary>Danh sách minh chứng sinh viên đã nộp cho hoạt động, mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<EvidenceResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvidences(int activityId, CancellationToken cancellationToken)
    {
        if (!TryGetStudentId(out var studentId))
        {
            return InvalidSession();
        }

        var result = await _evidenceService.GetEvidencesAsync(activityId, studentId, cancellationToken);
        if (!result.Success)
        {
            return ProblemFrom(result);
        }

        return Ok(result);
    }

    /// <summary>Nộp minh chứng tham gia hoạt động (hình ảnh, tài liệu hoặc mã xác nhận).</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponseDto<EvidenceResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadEvidence(
        int activityId,
        [FromForm] EvidenceUploadDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStudentId(out var studentId))
        {
            return InvalidSession();
        }

        var result = await _evidenceService.UploadAsync(activityId, studentId, dto, cancellationToken);
        if (!result.Success)
        {
            return ProblemFrom(result);
        }

        return Created($"/api/v1/student-activities/{activityId}/evidences", result);
    }

    /// <summary>Tải file minh chứng của chính sinh viên.</summary>
    [HttpGet("{evidenceId:int}/file")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(
        int activityId,
        int evidenceId,
        CancellationToken cancellationToken)
    {
        if (!TryGetStudentId(out var studentId))
        {
            return InvalidSession();
        }

        var result = await _evidenceService.OpenFileAsync(activityId, evidenceId, studentId, cancellationToken);
        if (!result.Success || result.Data == null)
        {
            return ProblemFrom(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>Xóa minh chứng chưa duyệt khi còn trong hạn nộp.</summary>
    [HttpDelete("{evidenceId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvidence(
        int activityId,
        int evidenceId,
        CancellationToken cancellationToken)
    {
        if (!TryGetStudentId(out var studentId))
        {
            return InvalidSession();
        }

        var result = await _evidenceService.DeleteAsync(activityId, evidenceId, studentId, cancellationToken);
        if (!result.Success)
        {
            return ProblemFrom(result);
        }

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
