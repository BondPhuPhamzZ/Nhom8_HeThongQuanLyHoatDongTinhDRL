using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>US-22: evaluation periods (kỳ đánh giá) that training points are recorded in.</summary>
[ApiController]
[Route("api/v1/admin/evaluation-periods")]
[Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class EvaluationPeriodsController : ApiControllerBase
{
    private readonly IEvaluationPeriodService _periodService;

    public EvaluationPeriodsController(IEvaluationPeriodService periodService)
    {
        _periodService = periodService;
    }

    /// <summary>Danh sách kỳ đánh giá, mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<EvaluationPeriodDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriods() => Ok(await _periodService.GetPeriodsAsync());

    /// <summary>Tạo kỳ đánh giá (không được trùng tên hoặc chồng thời gian với kỳ khác).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<EvaluationPeriodDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePeriod([FromBody] EvaluationPeriodWriteDto request)
    {
        var result = await _periodService.CreatePeriodAsync(request);
        return result.Success ? Created("/api/v1/admin/evaluation-periods", result) : ProblemFrom(result);
    }

    /// <summary>Sửa kỳ đánh giá; kỳ đã có điểm chỉ sửa được tên và điểm mục tiêu.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<EvaluationPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePeriod(int id, [FromBody] EvaluationPeriodWriteDto request)
    {
        var result = await _periodService.UpdatePeriodAsync(id, request);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Khóa (Closed) hoặc mở lại (Open) kỳ. Kỳ đã khóa không nhận ghi điểm mới.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ApiResponseDto<EvaluationPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] EvaluationPeriodStatusDto request)
    {
        var result = await _periodService.SetStatusAsync(id, request.Status);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
