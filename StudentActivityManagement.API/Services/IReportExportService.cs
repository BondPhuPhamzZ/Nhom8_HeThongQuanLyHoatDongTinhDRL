using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Reports;

namespace StudentActivityManagement.API.Services;

public sealed record ReportExportFile(byte[] Content, string FileName, int AtRiskRowCount, int PeriodId);

public interface IReportExportService
{
    Task<ApiResponseDto<ReportExportFile>> ExportAsync(ReportQueryDto query);
}
