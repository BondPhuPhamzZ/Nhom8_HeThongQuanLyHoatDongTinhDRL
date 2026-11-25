using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Services;

public sealed class ActivityLifecycleService(
    AppDbContext context,
    TimeProvider time,
    IConfiguration configuration,
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
                               activity.Status == ActivityStatus.Ongoing && (activity.EndTime <= now || activity.StartTime > now) ||
                               activity.Status == ActivityStatus.Completed && activity.PointsFinalizedAt == null && activity.EndTime <= now)
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
                if (activity?.Status is not (ActivityStatus.Published or ActivityStatus.Ongoing or ActivityStatus.Completed)) continue;

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
                if (status != activity.Status)
                {
                    var updated = await context.Activities.Where(item => item.Id == id && item.Status == activity.Status)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(item => item.Status, status)
                            .SetProperty(item => item.UpdatedAt, now), ct);
                    changed += updated;
                    if (updated == 1 && status == ActivityStatus.Published)
                        logger.LogWarning("Restored activity {ActivityId} to Published because its first session has not begun", id);
                }
                await transaction.CommitAsync(ct);
                await transaction.DisposeAsync();

                if (phase == ActivityPhase.AttendanceReview &&
                    now >= ActivityPhase.FinalizeDueAt(activity, activity.Sessions, config))
                    await FinalizeAsync(id, null, ct);
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

        return changed;
    }

    public async Task<ApiResponseDto<FinalizeResultDto>> FinalizeAsync(int activityId, int? staffId, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var activity = await context.Activities.AsNoTracking().Include(item => item.Sessions)
            .SingleOrDefaultAsync(item => item.Id == activityId, ct);
        if (activity == null)
            return ApiResponseDto<FinalizeResultDto>.Fail("Không tìm thấy hoạt động.", StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);

        if (activity.PointsFinalizedAt is DateTime already)
            return ApiResponseDto<FinalizeResultDto>.Ok(new FinalizeResultDto(0, 0, 0, already),
                "Hoạt động đã được chốt điểm trước đó.");

        var config = EffectiveCheckInConfig.From(activityId, await context.ActivityCheckInConfigs.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ActivityId == activityId, ct));
        var now = time.GetUtcNow().UtcDateTime;
        if (ActivityPhase.Resolve(activity, activity.Sessions, config, now) != ActivityPhase.AttendanceReview)
            return Blocked("Chỉ chốt điểm trong giai đoạn đối chiếu điểm danh.");

        var period = await context.EvaluationPeriods.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Status == EvaluationPeriodStatus.Open && item.StartDate <= activity.EndTime && activity.EndTime <= item.EndDate, ct);

        if (period == null)
        {
            var owners = await context.Users.AsNoTracking().Where(item => item.IsActive && item.Role == Role.HSSV)
                .Select(item => item.Id).ToListAsync(ct);
            var keys = owners.Select(item => $"finalize-blocked:{activityId}:{item}").ToList();
            var existing = await context.UserNotifications.AsNoTracking().Where(item => keys.Contains(item.SourceKey))
                .Select(item => item.SourceKey).ToListAsync(ct);
            foreach (var owner in owners)
            {
                var key = $"finalize-blocked:{activityId}:{owner}";
                if (!existing.Contains(key))
                    AddNotice(activityId, owner, key, "FinalizationBlocked", "Chưa có kỳ đánh giá mở",
                        $"Chưa chốt điểm hoạt động \"{activity.ActivityName}\" vì chưa có kỳ đánh giá mở.", now);
            }
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Blocked("Chưa có kỳ đánh giá mở để chốt điểm. Hệ thống sẽ thử lại khi kỳ được mở.");
        }

        try
        {
            var claimed = await context.Activities.Where(item => item.Id == activityId &&
                    item.PointsFinalizedAt == null && item.Status == activity.Status)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, ActivityStatus.Completed)
                    .SetProperty(item => item.UpdatedAt, now)
                    .SetProperty(item => item.PointsFinalizedAt, now)
                    .SetProperty(item => item.PointsFinalizedById, staffId), ct);
            if (claimed == 0)
            {
                var finalizedAt = await context.Activities.AsNoTracking().Where(item => item.Id == activityId)
                    .Select(item => item.PointsFinalizedAt).SingleAsync(ct);
                return finalizedAt.HasValue
                    ? ApiResponseDto<FinalizeResultDto>.Ok(new FinalizeResultDto(0, 0, 0, finalizedAt.Value), "Hoạt động đã được chốt điểm trước đó.")
                    : ApiResponseDto<FinalizeResultDto>.Fail("Trạng thái hoạt động đã thay đổi. Vui lòng thử lại.",
                        StatusCodes.Status409Conflict, ApiErrorCodes.ConcurrencyConflict);
            }

            var registrations = await context.ActivityRegistrations.Where(item => item.ActivityId == activityId &&
                item.Status != RegistrationStatus.Cancelled).ToListAsync(ct);
            var activeIds = registrations.Select(item => item.Id).ToList();
            var existingRecords = await context.PointRecords.AsNoTracking().Where(item => activeIds.Contains(item.RegistrationId))
                .ToDictionaryAsync(item => item.RegistrationId, ct);
            var attendedIds = (await context.ActivityCheckIns.AsNoTracking().Where(item => activeIds.Contains(item.RegistrationId))
                .Select(item => item.RegistrationId).Distinct().ToListAsync(ct)).ToHashSet();
            var approved = 0;
            var absent = 0;
            var deadline = ActivityService.VietnamTime(now.AddDays(PointAppealService.WindowDays(configuration)));
            foreach (var registration in registrations)
            {
                var key = $"points-finalized:{registration.Id}";
                var title = "Đã chốt điểm rèn luyện";
                if (existingRecords.TryGetValue(registration.Id, out var existingRecord))
                {
                    var result = existingRecord.Status == PointRecordStatus.Approved
                        ? $"Bạn đã được ghi {existingRecord.Points} điểm rèn luyện."
                        : "Bạn có thể xem quyết định hiện có trong lịch sử điểm.";
                    AddNotice(activityId, registration.StudentId, key, "PointsFinalized", title,
                        $"Đã chốt điểm: {activity.ActivityName}. {result}", now);
                    continue;
                }

                var attended = attendedIds.Contains(registration.Id);
                var status = attended ? PointRecordStatus.Approved : PointRecordStatus.Rejected;
                var reason = attended ? AttendancePoints.FinalizedReason : AttendancePoints.AbsentReason;
                var points = activity.TrainingPoints;
                registration.Status = attended ? RegistrationStatus.Attended : RegistrationStatus.Absent;
                registration.PointsEarned = attended ? points : 0;
                if (attended) approved++; else absent++;
                context.PointRecords.Add(new PointRecord
                {
                    ActivityId = activityId, StudentId = registration.StudentId, RegistrationId = registration.Id,
                    PeriodId = period.Id, Points = points, Status = status, Reason = reason,
                    DecidedAt = now, DecidedById = staffId,
                    History =
                    [
                        new PointRecordHistory { ToStatus = status, Points = points, Reason = reason, ChangedAt = now, ChangedById = staffId }
                    ]
                });
                var message = attended ? $"Bạn được cộng {points} điểm rèn luyện vào kỳ {period.Name}."
                    : $"Bạn không có dữ liệu điểm danh nên chưa được cộng điểm. Nếu bạn có tham gia, hãy gửi khiếu nại trước {deadline}.";
                AddNotice(activityId, registration.StudentId, key, "PointsFinalized", title,
                    $"Đã chốt điểm: {activity.ActivityName}. {message}", now);
            }
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ApiResponseDto<FinalizeResultDto>.Ok(new FinalizeResultDto(approved, absent, existingRecords.Count, now),
                "Đã chốt điểm rèn luyện.");
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(ct);
            context.ChangeTracker.Clear();
            logger.LogWarning(exception, "Could not finalize points for activity {ActivityId}", activityId);
            return ApiResponseDto<FinalizeResultDto>.Fail("Không thể chốt điểm vì dữ liệu đã thay đổi. Vui lòng thử lại.",
                StatusCodes.Status409Conflict, ApiErrorCodes.ConcurrencyConflict);
        }
    }

    private void AddNotice(int activityId, int recipientId, string key, string kind, string title, string body, DateTime now) =>
        context.UserNotifications.Add(new UserNotification
        {
            ActivityId = activityId, RecipientUserId = recipientId, SourceKey = key, Kind = kind,
            Title = title, Body = body, CreatedAt = now
        });

    private static ApiResponseDto<FinalizeResultDto> Blocked(string message) =>
        ApiResponseDto<FinalizeResultDto>.Fail(message, StatusCodes.Status409Conflict, ApiErrorCodes.FinalizationBlocked);
}
