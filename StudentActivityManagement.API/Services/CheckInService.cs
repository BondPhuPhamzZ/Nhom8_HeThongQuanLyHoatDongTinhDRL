using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.CheckIn;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Services;

public sealed class CheckInService : ICheckInService
{
    // No 0/O or 1/I/L so codes read aloud or projected on a screen are not mistyped.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;

    // Check-in happens while the activity runs; Published is accepted too so a session is not lost
    // when staff forget to switch the activity to Ongoing. The time window is the real gate.
    private static readonly string[] CheckInStatuses = [ActivityStatus.Published, ActivityStatus.Ongoing];

    private readonly AppDbContext _context;
    private readonly ICheckInConfigProvider _configProvider;

    public CheckInService(AppDbContext context, ICheckInConfigProvider configProvider)
    {
        _context = context;
        _configProvider = configProvider;
    }

    public async Task<ApiResponseDto<StudentCheckInStatusDto>> GetStudentStatusAsync(int activityId, int studentId)
    {
        var activity = await _context.Activities.AsNoTracking()
            .Include(item => item.Sessions)
            .FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return NotFound<StudentCheckInStatusDto>("Không tìm thấy hoạt động.");
        }

        var registration = await _context.ActivityRegistrations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ActivityId == activityId && item.StudentId == studentId);
        if (registration == null)
        {
            return NotFound<StudentCheckInStatusDto>("Bạn chưa đăng ký tham gia hoạt động này.");
        }

        var config = await _configProvider.GetAsync(activityId);
        var checkIns = await _context.ActivityCheckIns.AsNoTracking()
            .Where(item => item.RegistrationId == registration.Id)
            .ToDictionaryAsync(item => item.SessionId, item => item.CheckedInAt);
        var now = DateTime.UtcNow;
        var canUseCode = config.Method == CheckInMethods.Code &&
                         CheckInStatuses.Contains(activity.Status) &&
                         registration.Status != RegistrationStatus.Cancelled;

        return ApiResponseDto<StudentCheckInStatusDto>.Ok(new StudentCheckInStatusDto
        {
            ActivityId = activity.Id,
            ActivityStatus = activity.Status,
            Method = config.Method,
            RegistrationStatus = registration.Status,
            Sessions = activity.Sessions.OrderBy(session => session.SortOrder).Select(session => new SessionCheckInStatusDto
            {
                SessionId = session.Id,
                SessionName = session.SessionName,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                OpensAt = config.OpensAt(session),
                ClosesAt = config.ClosesAt(session),
                CheckedInAt = checkIns.TryGetValue(session.Id, out var checkedInAt) ? checkedInAt : null,
                CanCheckIn = canUseCode && !checkIns.ContainsKey(session.Id) && config.IsOpen(session, now)
            }).ToList()
        });
    }

    public async Task<ApiResponseDto<CheckInResultDto>> CheckInAsync(int activityId, int sessionId, int studentId, string code)
    {
        var activity = await _context.Activities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return NotFound<CheckInResultDto>("Không tìm thấy hoạt động.");
        }

        var session = await _context.ActivitySessions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.ActivityId == activityId);
        if (session == null)
        {
            return NotFound<CheckInResultDto>("Không tìm thấy buổi tổ chức của hoạt động này.");
        }

        var registration = await _context.ActivityRegistrations
            .FirstOrDefaultAsync(item => item.ActivityId == activityId && item.StudentId == studentId);
        if (registration == null)
        {
            return NotFound<CheckInResultDto>("Bạn chưa đăng ký tham gia hoạt động này.");
        }
        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return Conflict<CheckInResultDto>("Bạn đã hủy đăng ký hoạt động này nên không thể check-in.");
        }
        if (!CheckInStatuses.Contains(activity.Status))
        {
            return Conflict<CheckInResultDto>("Hoạt động không ở trạng thái cho phép check-in.");
        }

        var config = await _configProvider.GetAsync(activityId);
        if (config.Method != CheckInMethods.Code)
        {
            return Conflict<CheckInResultDto>("Hoạt động này điểm danh trực tiếp với ban tổ chức, không dùng mã check-in.");
        }

        var now = DateTime.UtcNow;
        if (now < config.OpensAt(session))
        {
            return Conflict<CheckInResultDto>("Chưa đến thời gian check-in của buổi này.");
        }
        if (now > config.ClosesAt(session))
        {
            return Conflict<CheckInResultDto>("Đã hết thời gian check-in của buổi này.");
        }

        if (await _context.ActivityCheckIns.AnyAsync(item => item.RegistrationId == registration.Id && item.SessionId == sessionId))
        {
            return Fail<CheckInResultDto>("Bạn đã check-in buổi này rồi.", StatusCodes.Status409Conflict, ApiErrorCodes.ResourceConflict);
        }
        if (string.IsNullOrEmpty(session.CheckInCode))
        {
            return Conflict<CheckInResultDto>("Ban tổ chức chưa phát mã check-in cho buổi này.");
        }
        if (!string.Equals(session.CheckInCode, code.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Fail<CheckInResultDto>("Mã check-in không đúng.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.ActivityCheckIns.Add(new ActivityCheckIn
            {
                RegistrationId = registration.Id,
                SessionId = sessionId,
                CheckedInAt = now,
                Method = CheckInMethods.Code
            });
            // Attending any session marks the registration Attended; per-session rows keep the detail for scoring.
            if (registration.Status == RegistrationStatus.Registered)
            {
                registration.Status = RegistrationStatus.Attended;
                registration.CheckInTime ??= now;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Two requests for the same session raced past the check above; the unique index kept one.
            await transaction.RollbackAsync();
            return Fail<CheckInResultDto>("Bạn đã check-in buổi này rồi.", StatusCodes.Status409Conflict, ApiErrorCodes.ResourceConflict);
        }

        return ApiResponseDto<CheckInResultDto>.Ok(new CheckInResultDto
        {
            ActivityId = activityId,
            SessionId = sessionId,
            SessionName = session.SessionName,
            CheckedInAt = now,
            Method = CheckInMethods.Code,
            RegistrationStatus = registration.Status
        }, $"Check-in thành công buổi \"{session.SessionName}\".");
    }

    public async Task<ApiResponseDto<ActivityCheckInCodesDto>> GetSessionCodesAsync(int activityId)
    {
        var activity = await _context.Activities.AsNoTracking()
            .Include(item => item.Sessions)
            .FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return NotFound<ActivityCheckInCodesDto>("Không tìm thấy hoạt động.");
        }

        var config = await _configProvider.GetAsync(activityId);
        var sessionIds = activity.Sessions.Select(session => session.Id).ToList();
        var counts = await _context.ActivityCheckIns.AsNoTracking()
            .Where(item => sessionIds.Contains(item.SessionId))
            .GroupBy(item => item.SessionId)
            .Select(group => new { SessionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SessionId, item => item.Count);

        return ApiResponseDto<ActivityCheckInCodesDto>.Ok(new ActivityCheckInCodesDto
        {
            ActivityId = activity.Id,
            ActivityStatus = activity.Status,
            Method = config.Method,
            OpenBeforeMinutes = config.OpenBeforeMinutes,
            CloseAfterMinutes = config.CloseAfterMinutes,
            Sessions = activity.Sessions.OrderBy(session => session.SortOrder)
                .Select(session => MapCode(session, config, counts.GetValueOrDefault(session.Id)))
                .ToList()
        });
    }

    public async Task<ApiResponseDto<SessionCheckInCodeDto>> GenerateSessionCodeAsync(int activityId, int sessionId)
    {
        var activity = await _context.Activities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return NotFound<SessionCheckInCodeDto>("Không tìm thấy hoạt động.");
        }

        var session = await _context.ActivitySessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.ActivityId == activityId);
        if (session == null)
        {
            return NotFound<SessionCheckInCodeDto>("Không tìm thấy buổi tổ chức của hoạt động này.");
        }
        if (!CheckInStatuses.Contains(activity.Status))
        {
            return Conflict<SessionCheckInCodeDto>("Chỉ phát mã check-in cho hoạt động đang mở hoặc đang diễn ra.");
        }

        var config = await _configProvider.GetAsync(activityId);
        if (config.Method != CheckInMethods.Code)
        {
            return Conflict<SessionCheckInCodeDto>("Hoạt động đang cấu hình điểm danh thủ công, không dùng mã check-in.");
        }

        // A new code replaces the old one, so a code that leaked can be revoked during the session.
        session.CheckInCode = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
        session.CheckInCodeGeneratedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var count = await _context.ActivityCheckIns.CountAsync(item => item.SessionId == sessionId);
        return ApiResponseDto<SessionCheckInCodeDto>.Ok(
            MapCode(session, config, count),
            $"Đã tạo mã check-in mới cho buổi \"{session.SessionName}\".");
    }

    private static SessionCheckInCodeDto MapCode(ActivitySession session, EffectiveCheckInConfig config, int checkInCount) => new()
    {
        SessionId = session.Id,
        SessionName = session.SessionName,
        StartTime = session.StartTime,
        EndTime = session.EndTime,
        OpensAt = config.OpensAt(session),
        ClosesAt = config.ClosesAt(session),
        Code = session.CheckInCode,
        CodeGeneratedAt = session.CheckInCodeGeneratedAt,
        CheckInCount = checkInCount
    };

    private static ApiResponseDto<T> NotFound<T>(string message) =>
        Fail<T>(message, StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);

    private static ApiResponseDto<T> Conflict<T>(string message) =>
        Fail<T>(message, StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);

    private static ApiResponseDto<T> Fail<T>(string message, int status, string errorCode) =>
        ApiResponseDto<T>.Fail(message, status, errorCode);
}
