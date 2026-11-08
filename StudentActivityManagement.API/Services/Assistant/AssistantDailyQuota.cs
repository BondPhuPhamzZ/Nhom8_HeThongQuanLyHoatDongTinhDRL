using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services.Assistant;

/// <summary>
/// Daily question limit per student (US-32), stored in <see cref="AssistantDailyUsage"/>. The per-minute rate limiter
/// stops bursts; this stops one account from using up the project's shared free Gemini quota over a day.
/// </summary>
public sealed class AssistantDailyQuota
{
    private readonly AppDbContext _context;
    private readonly IOptions<AssistantOptions> _options;

    public AssistantDailyQuota(AppDbContext context, IOptions<AssistantOptions> options)
    {
        _context = context;
        _options = options;
    }

    public int Limit => Math.Max(1, _options.Value.DailyQuestionLimit);

    /// <summary>Vietnam calendar day (UTC+7, no daylight saving).</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    public async Task<int> RemainingAsync(int studentId)
    {
        var today = Today;
        var used = await _context.AssistantDailyUsages.AsNoTracking()
            .Where(usage => usage.StudentId == studentId && usage.UsageDate == today)
            .Select(usage => (int?)usage.QuestionCount)
            .FirstOrDefaultAsync() ?? 0;
        return Math.Max(0, Limit - used);
    }

    /// <summary>Counts one question; false when the student already used today's limit.</summary>
    public async Task<bool> TryConsumeAsync(int studentId)
    {
        var today = Today;
        var limit = Limit;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            // Atomic "count + 1 while below the limit" in one UPDATE, so parallel requests cannot pass the limit.
            var updated = await _context.AssistantDailyUsages
                .Where(usage => usage.StudentId == studentId && usage.UsageDate == today && usage.QuestionCount < limit)
                .ExecuteUpdateAsync(setters => setters.SetProperty(usage => usage.QuestionCount, usage => usage.QuestionCount + 1));
            if (updated == 1) return true;

            var exists = await _context.AssistantDailyUsages.AnyAsync(usage => usage.StudentId == studentId && usage.UsageDate == today);
            if (exists) return false;

            try
            {
                _context.AssistantDailyUsages.Add(new AssistantDailyUsage { StudentId = studentId, UsageDate = today, QuestionCount = 1 });
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                // Another request created today's row first; detach ours and count with the UPDATE above.
                _context.ChangeTracker.Clear();
            }
        }

        return false;
    }

    /// <summary>Gives the question back when Gemini could not answer (quota, outage), so failures do not cost the student.</summary>
    public Task RefundAsync(int studentId)
    {
        var today = Today;
        return _context.AssistantDailyUsages
            .Where(usage => usage.StudentId == studentId && usage.UsageDate == today && usage.QuestionCount > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(usage => usage.QuestionCount, usage => usage.QuestionCount - 1));
    }
}
