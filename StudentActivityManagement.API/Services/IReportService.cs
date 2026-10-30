using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Reports;

namespace StudentActivityManagement.API.Services;

/// <summary>US-24: activity, registration, participation and point reports per period for Phòng QL HSSV.</summary>
public interface IReportService
{
    Task<ApiResponseDto<ReportOverviewDto>> GetOverviewAsync(ReportQueryDto query);

    Task<ApiResponseDto<List<ActivityReportRowDto>>> GetActivitiesAsync(ReportQueryDto query);

    Task<ApiResponseDto<PagedResultDto<AtRiskStudentDto>>> GetAtRiskStudentsAsync(ReportQueryDto query, int pageIndex, int pageSize);
}
