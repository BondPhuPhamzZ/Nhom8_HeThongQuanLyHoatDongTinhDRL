using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services.Assistant;

public sealed record AssistantUsageLease(int UserId, DateOnly UsageDate, string Feature);

public sealed class AssistantDailyQuota(AppDbContext context, IOptions<AssistantOptions> options, TimeProvider time)
{
    public int Limit => Math.Max(1, options.Value.DailyQuestionLimit);
    public int TokenLimit => Math.Max(1, options.Value.DailyTokenLimit);
    public DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime.AddHours(7));

    public async Task<(int Remaining, int TokensUsed)> GetStudentStatusAsync(int studentId)
    {
        var row = await Find(studentId, Today, AssistantDailyUsage.Assistant)
            .AsNoTracking().SingleOrDefaultAsync();
        return (Math.Max(0, Limit - (row?.QuestionCount ?? 0)), row?.TokenCount ?? 0);
    }

    public async Task<ApiResponseDto<AssistantUsageLease>> TryConsumeAsync(int userId, string feature, CancellationToken ct)
    {
        var date = Today;
        var limits = options.Value;
        // Serialize the global check and reservation, including the first row for a user.
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var total = await context.AssistantDailyUsages.Where(item => item.UsageDate == date)
            .SumAsync(item => (long)item.QuestionCount, ct);
        if (total >= Math.Max(1, limits.GlobalDailyRequestLimit))
            return Limited("Trợ lý AI đã đạt giới hạn sử dụng chung của hôm nay.");

        var usage = Find(userId, date, feature);
        var row = await usage.AsNoTracking().SingleOrDefaultAsync(ct);
        if (row != null && row.TokenCount >= TokenLimit)
            return Limited("Bạn đã dùng hết hạn mức AI của hôm nay.");

        var requestLimit = feature == AssistantDailyUsage.StaffAi ? Math.Max(1, limits.StaffDailyRequestLimit) : Limit;
        if (row != null && row.QuestionCount >= requestLimit)
            return Limited($"Bạn đã dùng hết {requestLimit} lượt AI của hôm nay. Vui lòng thử lại vào ngày mai.");

        if (row == null)
        {
            context.AssistantDailyUsages.Add(new AssistantDailyUsage
            {
                StudentId = userId, UsageDate = date, Feature = feature, QuestionCount = 1
            });
            await context.SaveChangesAsync(ct);
        }
        else
        {
            await usage.ExecuteUpdateAsync(setters =>
                setters.SetProperty(item => item.QuestionCount, item => item.QuestionCount + 1), ct);
        }
        await tx.CommitAsync(ct);
        return ApiResponseDto<AssistantUsageLease>.Ok(new AssistantUsageLease(userId, date, feature));
    }

    public Task FinishAsync(AssistantUsageLease lease, bool hasSuccessfulRound, int tokens) =>
        Find(lease.UserId, lease.UsageDate, lease.Feature)
            .Where(item => item.QuestionCount > 0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.QuestionCount, item => item.QuestionCount - (hasSuccessfulRound ? 0 : 1))
                .SetProperty(item => item.TokenCount, item => item.TokenCount + tokens));

    private IQueryable<AssistantDailyUsage> Find(int userId, DateOnly date, string feature) =>
        context.AssistantDailyUsages.Where(item => item.StudentId == userId && item.UsageDate == date && item.Feature == feature);

    private static ApiResponseDto<AssistantUsageLease> Limited(string message) =>
        ApiResponseDto<AssistantUsageLease>.Fail(message, StatusCodes.Status429TooManyRequests, ApiErrorCodes.AssistantRateLimited);
}
