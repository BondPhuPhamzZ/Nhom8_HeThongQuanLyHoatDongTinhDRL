using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Student;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Services;

public sealed class StudentReportService : IStudentReportService
{
    private static readonly string[] RegistrationStatuses =
        [RegistrationStatus.Registered, RegistrationStatus.Attended, RegistrationStatus.Cancelled, RegistrationStatus.Absent];

    private readonly AppDbContext _context;
    private readonly TimeProvider _time;
    private readonly int _appealWindowDays;

    public StudentReportService(AppDbContext context, IConfiguration configuration, TimeProvider time)
    {
        _context = context;
        _time = time;
        _appealWindowDays = PointAppealService.WindowDays(configuration);
    }

    public async Task<ApiResponseDto<List<StudentEvaluationPeriodDto>>> GetPeriodsAsync(int studentId)
    {
        var periods = await _context.EvaluationPeriods.AsNoTracking()
            .OrderByDescending(period => period.StartDate)
            .Select(period => new StudentEvaluationPeriodDto
            {
                Id = period.Id,
                Name = period.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                TargetPoints = period.TargetPoints,
                Status = period.Status,
                ApprovedPoints = _context.PointRecords
                    .Where(record => record.StudentId == studentId && record.PeriodId == period.Id && record.Status == PointRecordStatus.Approved)
                    .Sum(record => (int?)record.Points) ?? 0
            })
            .ToListAsync();
        return ApiResponseDto<List<StudentEvaluationPeriodDto>>.Ok(periods);
    }

    public async Task<ApiResponseDto<StudentEvaluationHistoryDto>> GetActivityHistoryAsync(
        int studentId, int? periodId, string? registrationStatus)
    {
        string? normalizedStatus = null;
        if (!string.IsNullOrWhiteSpace(registrationStatus))
        {
            normalizedStatus = RegistrationStatuses.FirstOrDefault(status =>
                status.Equals(registrationStatus.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalizedStatus == null)
            {
                return Fail<StudentEvaluationHistoryDto>(
                    "Trạng thái đăng ký không hợp lệ.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
            }
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var period = periodId.HasValue
            ? await _context.EvaluationPeriods.AsNoTracking().FirstOrDefaultAsync(item => item.Id == periodId.Value)
            : await _context.EvaluationPeriods.AsNoTracking()
                .OrderByDescending(item => item.StartDate <= now && item.EndDate >= now)
                .ThenByDescending(item => item.StartDate)
                .FirstOrDefaultAsync();
        if (period == null) return Fail<StudentEvaluationHistoryDto>(
            "Không tìm thấy kỳ đánh giá.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);

        var query = _context.ActivityRegistrations.AsNoTracking()
            .Where(registration => registration.StudentId == studentId &&
                registration.Activity!.Sessions.Any(session => session.StartTime <= period.EndDate && session.EndTime >= period.StartDate));
        if (normalizedStatus != null) query = query.Where(registration => registration.Status == normalizedStatus);

        var registrations = await query
            .Include(registration => registration.Activity!)
                .ThenInclude(activity => activity.Sessions)
            .OrderByDescending(registration => registration.Activity!.Sessions
                .Where(session => session.StartTime <= period.EndDate && session.EndTime >= period.StartDate)
                .Max(session => session.StartTime))
            .ThenByDescending(registration => registration.RegisteredAt)
            .ToListAsync();

        var registrationIds = registrations.Select(item => item.Id).ToList();
        var records = await _context.PointRecords.AsNoTracking()
            .Where(item => item.StudentId == studentId && item.PeriodId == period.Id && registrationIds.Contains(item.RegistrationId))
            .ToDictionaryAsync(item => item.RegistrationId);
        var recordIds = records.Values.Select(item => item.Id).ToList();
        var appeals = await _context.PointAppeals.AsNoTracking()
            .Where(item => item.StudentId == studentId && recordIds.Contains(item.PointRecordId))
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .ToListAsync();
        var latestAppealByRecord = appeals.GroupBy(item => item.PointRecordId)
            .ToDictionary(group => group.Key, group => group.First().Status);
        var checkIns = await _context.ActivityCheckIns.AsNoTracking()
            .Include(item => item.Session)
            .Where(item => registrationIds.Contains(item.RegistrationId) && item.Session!.StartTime <= period.EndDate && item.Session.EndTime >= period.StartDate)
            .ToListAsync();
        var checkInsByRegistration = checkIns.GroupBy(item => item.RegistrationId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(item => item.SessionId));

        var activityIds = registrations.Select(item => item.ActivityId).Distinct().ToList();
        var configs = await _context.ActivityCheckInConfigs.AsNoTracking()
            .Where(item => activityIds.Contains(item.ActivityId)).ToDictionaryAsync(item => item.ActivityId);
        var openPeriods = await _context.EvaluationPeriods.AsNoTracking().Where(item => item.Status == EvaluationPeriodStatus.Open)
            .Select(item => new { item.StartDate, item.EndDate }).ToListAsync();

        var history = registrations.Select(registration =>
        {
            records.TryGetValue(registration.Id, out var pointRecord);
            var appealStatus = pointRecord != null && latestAppealByRecord.TryGetValue(pointRecord.Id, out var status)
                ? status : null;
            checkInsByRegistration.TryGetValue(registration.Id, out var attendance);
            var sessions = registration.Activity!.Sessions
                .Where(session => session.StartTime <= period.EndDate && session.EndTime >= period.StartDate)
                .OrderBy(session => session.StartTime)
                .Select(session =>
                {
                    ActivityCheckIn? checkIn = null;
                    if (attendance != null) attendance.TryGetValue(session.Id, out checkIn);
                    return new StudentSessionHistoryDto
                    {
                        SessionId = session.Id,
                        SessionName = session.SessionName,
                        StartTime = session.StartTime,
                        EndTime = session.EndTime,
                        CheckedInAt = checkIn?.CheckedInAt,
                        CheckInMethod = checkIn?.Method
                    };
                }).ToList();

            var activity = registration.Activity;
            var config = EffectiveCheckInConfig.From(activity.Id, configs.GetValueOrDefault(activity.Id));
            var dueAt = ActivityPhase.FinalizeDueAt(activity, activity.Sessions, config);
            var phase = ActivityPhase.Resolve(activity, activity.Sessions, config, now);
            var blocked = phase == ActivityPhase.AttendanceReview &&
                !openPeriods.Any(item => item.StartDate <= activity.EndTime && activity.EndTime <= item.EndDate);
            var canAppeal = PointAppealService.CanAppeal(pointRecord, period.Status, appealStatus, now, _appealWindowDays);
            var label = pointRecord?.Status switch
            {
                PointRecordStatus.Approved => $"Đã cộng {pointRecord.Points} điểm",
                PointRecordStatus.Revoked => "Đã hủy điểm",
                PointRecordStatus.Rejected when pointRecord.Reason == AttendancePoints.AbsentReason && canAppeal =>
                    $"Vắng: có thể khiếu nại đến {ActivityService.VietnamTime(pointRecord.DecidedAt.AddDays(_appealWindowDays))}",
                PointRecordStatus.Rejected when pointRecord.Reason == AttendancePoints.AbsentReason => "Vắng khi chốt điểm",
                PointRecordStatus.Rejected => "Bị từ chối",
                PointRecordStatus.Pending => "Chờ kiểm tra",
                _ when registration.Status == RegistrationStatus.Cancelled || phase == ActivityPhase.Cancelled => null,
                _ when blocked => "Đang chờ Phòng QL HSSV mở kỳ đánh giá",
                _ when activity.PointsFinalizedAt == null && registration.Status == RegistrationStatus.Attended =>
                    $"Chờ chốt điểm (dự kiến {ActivityService.VietnamTime(dueAt)})",
                _ => null
            };

            return new StudentActivityHistoryDto
            {
                RegistrationId = registration.Id,
                ActivityId = registration.ActivityId,
                ActivityName = registration.Activity.ActivityName,
                OrganizerName = registration.Activity.OrganizerName,
                RegistrationStatus = registration.Status,
                RegisteredAt = registration.RegisteredAt,
                TrainingPoints = registration.Activity.TrainingPoints,
                PointStatus = pointRecord?.Status,
                PointStatusLabel = label,
                PointsFinalizeDueAt = dueAt,
                PointsFinalizedAt = activity.PointsFinalizedAt,
                PointRecordId = pointRecord?.Id,
                AppealStatus = appealStatus,
                CanAppeal = canAppeal,
                PointsAwarded = pointRecord?.Points,
                PointReason = pointRecord?.Reason,
                PointDecidedAt = pointRecord?.DecidedAt,
                CheckInCount = sessions.Count(session => session.CheckedInAt.HasValue),
                Sessions = sessions
            };
        }).ToList();

        var approvedPoints = await _context.PointRecords.AsNoTracking()
            .Where(item => item.StudentId == studentId && item.PeriodId == period.Id && item.Status == PointRecordStatus.Approved)
            .SumAsync(item => (int?)item.Points) ?? 0;

        return ApiResponseDto<StudentEvaluationHistoryDto>.Ok(new StudentEvaluationHistoryDto
        {
            Period = new StudentEvaluationPeriodDto
            {
                Id = period.Id,
                Name = period.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                TargetPoints = period.TargetPoints,
                Status = period.Status,
                ApprovedPoints = approvedPoints
            },
            Activities = history
        });
    }

    private static ApiResponseDto<T> Fail<T>(string message, int status, string code) => ApiResponseDto<T>.Fail(message, status, code);
}
