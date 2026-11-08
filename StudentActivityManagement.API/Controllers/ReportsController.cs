using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Assistant;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Reports;
using StudentActivityManagement.API.Services;
using StudentActivityManagement.API.Services.Assistant;

namespace StudentActivityManagement.API.Controllers;

/// <summary>
/// US-24: reports per evaluation period, filtered by campus, cohort (academicYear) and class.
/// Without periodId the open period containing today is used, otherwise the most recent period.
/// </summary>
[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IReportService _reportService;
    private readonly IReportExportService _exportService;
    private readonly IStaffAiService _staffAi;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IReportService reportService, IReportExportService exportService,
        IStaffAiService staffAi, ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _exportService = exportService;
        _staffAi = staffAi;
        _logger = logger;
    }

    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] ReportQueryDto query)
    {
        var result = await _exportService.ExportAsync(query);
        if (!result.Success || result.Data == null) return ProblemFrom(result);

        _logger.LogInformation("Report exported by user {UserId} for period {PeriodId}: {AtRiskRows} at-risk rows",
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value, result.Data.PeriodId, result.Data.AtRiskRowCount);
        return File(result.Data.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }

    /// <summary>
    /// US-33: nhận xét bằng AI cho báo cáo đang lọc (điểm nổi bật, vấn đề, đề xuất). Mỗi ý chỉ được dùng số liệu có trong
    /// báo cáo; ý có số lạ bị loại và đếm vào removedStatements. Không gửi tên hay mã sinh viên lên Gemini.
    /// </summary>
    [HttpPost("ai-insights")]
    [EnableRateLimiting(AssistantService.RateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponseDto<ReportInsightsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAiInsights([FromQuery] ReportQueryDto query)
    {
        var result = await _staffAi.GetReportInsightsAsync(query, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>
    /// Số hoạt động, đăng ký, tham gia, tỷ lệ tham gia, tổng điểm, số sinh viên đạt/thiếu điểm mục tiêu,
    /// kèm bảng chia theo lớp, khóa hoặc cơ sở (groupBy).
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponseDto<ReportOverviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOverview([FromQuery] ReportQueryDto query)
    {
        var result = await _reportService.GetOverviewAsync(query);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Từng hoạt động trong kỳ: đăng ký, tham gia, tỷ lệ và điểm đã ghi (theo sinh viên trong phạm vi lọc).</summary>
    [HttpGet("activities")]
    [ProducesResponseType(typeof(ApiResponseDto<List<ActivityReportRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivities([FromQuery] ReportQueryDto query)
    {
        var result = await _reportService.GetActivitiesAsync(query);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>Sinh viên có điểm trong kỳ thấp hơn điểm mục tiêu, thiếu nhiều nhất trước.</summary>
    [HttpGet("at-risk-students")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<AtRiskStudentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAtRiskStudents([FromQuery] ReportQueryDto query, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _reportService.GetAtRiskStudentsAsync(query, pageIndex, pageSize);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
