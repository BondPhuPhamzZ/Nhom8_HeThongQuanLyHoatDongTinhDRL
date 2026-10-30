using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Notifications;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Services;

public sealed class NotificationService : INotificationService
{
    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context) => _context = context;

    public async Task<ApiResponseDto<PagedResultDto<UserNotificationDto>>> GetMineAsync(int userId, bool unreadOnly, int pageIndex, int pageSize)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = _context.UserNotifications.AsNoTracking().Where(item => item.RecipientUserId == userId);
        if (unreadOnly) query = query.Where(item => item.ReadAt == null);
        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
        return ApiResponseDto<PagedResultDto<UserNotificationDto>>.Ok(new PagedResultDto<UserNotificationDto>
        {
            Items = items.Select(Map).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponseDto<UserNotificationDto>> MarkReadAsync(int userId, int notificationId)
    {
        var item = await _context.UserNotifications.FirstOrDefaultAsync(value => value.Id == notificationId && value.RecipientUserId == userId);
        if (item == null)
        {
            return ApiResponseDto<UserNotificationDto>.Fail("Không tìm thấy thông báo của bạn.",
                StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }
        if (item.ReadAt == null)
        {
            item.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return ApiResponseDto<UserNotificationDto>.Ok(Map(item));
    }

    public async Task<int> DispatchDueRemindersAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Reminder dispatch requires UTC time.", nameof(nowUtc));

        var sessions = await _context.ActivitySessions.AsNoTracking()
            .Include(session => session.Activity).ThenInclude(activity => activity!.AudienceRules)
            .Where(session => session.StartTime > nowUtc && session.StartTime <= nowUtc.AddMinutes(SettingDefaults.MaxReminderOffsetMinutes)
                && (session.Activity!.Status == ActivityStatus.Published || session.Activity.Status == ActivityStatus.Ongoing))
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0) return 0;

        var students = await _context.Users.AsNoTracking()
            .Where(user => user.IsActive && (user.Role == Role.Student || user.Role == Role.Monitor))
            .ToListAsync(cancellationToken);
        var activityIds = sessions.Select(session => session.ActivityId).Distinct().ToArray();
        var registrations = await _context.ActivityRegistrations.AsNoTracking()
            .Where(registration => activityIds.Contains(registration.ActivityId)
                && (registration.Status == "Registered" || registration.Status == "Attended"))
            .Select(registration => new { registration.ActivityId, registration.StudentId })
            .ToListAsync(cancellationToken);
        var registered = registrations.Select(item => (item.ActivityId, item.StudentId)).ToHashSet();
        var offsets = await _context.ActivityReminderOffsets.AsNoTracking()
            .Where(offset => activityIds.Contains(offset.ActivityId))
            .ToListAsync(cancellationToken);
        var offsetsByActivity = offsets.GroupBy(item => item.ActivityId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.OffsetMinutes).Distinct().ToArray());
        var created = 0;

        foreach (var session in sessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var activity = session.Activity!;
            var reminderOffsets = offsetsByActivity.TryGetValue(activity.Id, out var configured)
                ? configured : SettingDefaults.ReminderOffsetsMinutes;
            foreach (var offset in reminderOffsets)
            {
                if (offset is < SettingDefaults.MinReminderOffsetMinutes or > SettingDefaults.MaxReminderOffsetMinutes
                    || session.StartTime.AddMinutes(-offset) > nowUtc) continue;

                foreach (var student in students)
                {
                    var isRequired = activity.AudienceRules.Any(rule => rule.ParticipationType == AudienceParticipationType.Required
                        && (rule.Campus == null || rule.Campus == student.Campus)
                        && (rule.AcademicYear == null || rule.AcademicYear == student.AcademicYear)
                        && (rule.ClassId == null || rule.ClassId == student.ClassId));
                    if (!registered.Contains((activity.Id, student.Id)) && !isRequired) continue;

                    var sourceKey = $"ActivityReminder:{session.Id}:{offset}:{student.Id}";
                    if (await _context.UserNotifications.AnyAsync(item => item.SourceKey == sourceKey, cancellationToken)) continue;
                    var notification = new UserNotification
                    {
                        RecipientUserId = student.Id,
                        SourceKey = sourceKey,
                        ActivityId = activity.Id,
                        SessionId = session.Id,
                        Title = $"Sắp diễn ra: {activity.ActivityName}",
                        Body = BuildBody(session, activity),
                        CreatedAt = nowUtc
                    };
                    _context.UserNotifications.Add(notification);
                    try
                    {
                        await _context.SaveChangesAsync(cancellationToken);
                        created++;
                    }
                    catch (DbUpdateException exception) when (exception.InnerException is SqliteException sqlite
                        && sqlite.SqliteErrorCode == 19 && sqlite.SqliteExtendedErrorCode == 2067)
                    {
                        // Another dispatcher inserted this exact reminder first.
                        _context.Entry(notification).State = EntityState.Detached;
                    }
                }
            }
        }
        return created;
    }

    private static string BuildBody(ActivitySession session, Activity activity)
    {
        var local = session.StartTime.AddHours(7); // The project displays Vietnam time (UTC+7).
        var body = $"Buổi {session.SessionName} bắt đầu lúc {local:HH:mm dd/MM/yyyy} tại {session.Location}, {session.Campus}.";
        if (!string.IsNullOrWhiteSpace(activity.Requirements)) body += $" Cần chuẩn bị: {activity.Requirements}";
        return body;
    }

    private static UserNotificationDto Map(UserNotification item) => new()
    {
        Id = item.Id,
        Kind = item.Kind,
        ActivityId = item.ActivityId,
        SessionId = item.SessionId,
        Title = item.Title,
        Body = item.Body,
        CreatedAt = item.CreatedAt,
        ReadAt = item.ReadAt
    };
}
