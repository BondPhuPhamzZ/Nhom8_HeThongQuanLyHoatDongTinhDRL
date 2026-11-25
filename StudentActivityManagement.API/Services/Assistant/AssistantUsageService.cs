using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Assistant;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services.Assistant;

public interface IAssistantUsageService
{
    Task<ApiResponseDto<AssistantUsageReportDto>> ReportAsync(DateOnly? from, DateOnly? to, string? keyword, CancellationToken ct);
}

public sealed class AssistantUsageService(AppDbContext context, AssistantDailyQuota quota) : IAssistantUsageService
{
    public async Task<ApiResponseDto<AssistantUsageReportDto>> ReportAsync(
        DateOnly? from, DateOnly? to, string? keyword, CancellationToken ct)
    {
        var end = to ?? quota.Today;
        var start = from ?? (end.DayNumber >= 6 ? end.AddDays(-6) : DateOnly.MinValue);
        if (start > end || end.DayNumber - start.DayNumber >= 31)
            return ApiResponseDto<AssistantUsageReportDto>.Fail("Chọn khoảng ngày hợp lệ, tối đa 31 ngày.",
                StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);

        var query = context.AssistantDailyUsages.AsNoTracking()
            .Where(item => item.UsageDate >= start && item.UsageDate <= end);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim().ToLowerInvariant();
            query = query.Where(item => item.Student!.StudentCode.ToLower().Contains(normalized) ||
                item.Student.FullName.ToLower().Contains(normalized));
        }
        var rows = await query.OrderByDescending(item => item.UsageDate).ThenBy(item => item.Student!.StudentCode)
            .ThenBy(item => item.Feature)
            .Select(item => new AssistantUsageRowDto(item.UsageDate, item.Feature, item.Student!.StudentCode,
                item.Student.FullName, item.Student.Role, item.QuestionCount, item.TokenCount))
            .ToListAsync(ct);
        var totals = rows.GroupBy(item => item.UsageDate).Select(group =>
            new AssistantUsageDailyTotalDto(group.Key, group.Sum(item => (long)item.QuestionCount),
                group.Sum(item => (long)item.TokenCount))).ToList();
        return ApiResponseDto<AssistantUsageReportDto>.Ok(new AssistantUsageReportDto(start, end, rows, totals));
    }
}
