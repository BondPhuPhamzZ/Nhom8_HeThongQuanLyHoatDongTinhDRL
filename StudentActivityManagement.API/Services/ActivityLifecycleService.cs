using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Services;

public sealed class ActivityLifecycleService(
    AppDbContext context,
    TimeProvider time,
    ILogger<ActivityLifecycleService> logger) : IActivityLifecycleService
{
    public ActivityPhaseInfo GetPhase(Activity activity, IEnumerable<ActivitySession> sessions, EffectiveCheckInConfig config)
    {
        var closesAt = ActivityPhase.CheckInClosesAt(activity, sessions, config);
        return new ActivityPhaseInfo(
            ActivityPhase.Resolve(activity, sessions, config, time.GetUtcNow().UtcDateTime),
            closesAt,
            closesAt.AddMinutes(config.CorrectionWindowMinutes));
    }

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var ids = await context.Activities.AsNoTracking()
            .Where(activity => activity.Status == ActivityStatus.Published && activity.StartTime <= now ||
                               activity.Status == ActivityStatus.Ongoing && (activity.EndTime <= now || activity.StartTime > now))
            .Select(activity => activity.Id)
            .ToListAsync(ct);

        var changed = 0;
        foreach (var id in ids)
        {
            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                var activity = await context.Activities.AsNoTracking().Include(item => item.Sessions)
                    .SingleOrDefaultAsync(item => item.Id == id, ct);
                if (activity?.Status is not (ActivityStatus.Published or ActivityStatus.Ongoing)) continue;

                var storedConfig = await context.ActivityCheckInConfigs.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.ActivityId == id, ct);
                var config = EffectiveCheckInConfig.From(id, storedConfig);
                var phase = ActivityPhase.Resolve(activity, activity.Sessions, config, now);
                var status = phase switch
                {
                    ActivityPhase.Ongoing => ActivityStatus.Ongoing,
                    ActivityPhase.AttendanceReview => ActivityStatus.Completed,
                    ActivityPhase.Upcoming or ActivityPhase.RegistrationOpen or ActivityPhase.RegistrationClosed
                        when activity.Status == ActivityStatus.Ongoing => ActivityStatus.Published,
                    _ => activity.Status
                };
                if (status == activity.Status) continue;
                var updated = await context.Activities.Where(item => item.Id == id && item.Status == activity.Status)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, status)
                        .SetProperty(item => item.UpdatedAt, now), ct);
                await transaction.CommitAsync(ct);
                changed += updated;
                if (updated == 1 && status == ActivityStatus.Published)
                    logger.LogWarning("Restored activity {ActivityId} to Published because its first session has not begun", id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not advance lifecycle for activity {ActivityId}", id);
            }
        }

        // US-47 adds point finalization for activities that have finished attendance review.
        return changed;
    }
}
