using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Reports;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

/// <summary>
/// Reports are computed from one in-memory snapshot of the period (students in scope, their registrations
/// in the period's activities and their approved points). Data per period is small enough for that, and it
/// keeps every report on exactly the same definitions.
/// </summary>
public sealed class ReportService : IReportService
{
    private const string GroupByClass = "class";
    private const string GroupByAcademicYear = "academicYear";
    private const string GroupByCampus = "campus";
    private static readonly string[] GroupByOptions = [GroupByClass, GroupByAcademicYear, GroupByCampus];

    // Draft activities never ran and cancelled ones are reported separately, so neither counts in the metrics.
    private static readonly string[] CountedStatuses = [ActivityStatus.Published, ActivityStatus.Ongoing, ActivityStatus.Completed];

    private readonly AppDbContext _context;

    public ReportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponseDto<ReportOverviewDto>> GetOverviewAsync(ReportQueryDto query)
    {
        var groupBy = GroupByOptions.FirstOrDefault(option => option.Equals(query.GroupBy ?? GroupByClass, StringComparison.OrdinalIgnoreCase));
        if (groupBy == null)
        {
            return Fail<ReportOverviewDto>("groupBy chỉ nhận class, academicYear hoặc campus.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }

        var (error, snapshot) = await LoadSnapshotAsync<ReportOverviewDto>(query);
        if (error != null)
        {
            return error;
        }

        var target = snapshot!.Period.TargetPoints;
        var groups = snapshot.Students
            .GroupBy(student => groupBy switch
            {
                GroupByAcademicYear => (Key: student.AcademicYear, Label: student.AcademicYear),
                GroupByCampus => (Key: student.Campus, Label: student.Campus),
                _ => (Key: student.ClassId?.ToString() ?? string.Empty, Label: student.ClassCode ?? "Chưa xếp lớp")
            })
            .Select(group => BuildGroup(group.Key.Key, group.Key.Label, group.ToList(), snapshot, target))
            .OrderBy(row => row.Label)
            .ToList();
        var total = BuildGroup(string.Empty, string.Empty, snapshot.Students, snapshot, target);

        return ApiResponseDto<ReportOverviewDto>.Ok(new ReportOverviewDto
        {
            Period = snapshot.Period,
            ActivityCount = snapshot.Activities.Count,
            CancelledActivityCount = snapshot.CancelledActivityCount,
            StudentCount = total.StudentCount,
            RegistrationCount = total.RegistrationCount,
            AttendedCount = total.AttendedCount,
            ParticipationRate = total.ParticipationRate,
            ApprovedPoints = total.ApprovedPoints,
            AveragePoints = total.AveragePoints,
            StudentsMeetingTarget = total.StudentCount - total.AtRiskCount,
            AtRiskCount = total.AtRiskCount,
            GroupBy = groupBy,
            Groups = groups
        });
    }

    public async Task<ApiResponseDto<List<ActivityReportRowDto>>> GetActivitiesAsync(ReportQueryDto query)
    {
        var (error, snapshot) = await LoadSnapshotAsync<List<ActivityReportRowDto>>(query);
        if (error != null)
        {
            return error;
        }

        var studentIds = snapshot!.Students.Select(student => student.Id).ToHashSet();
        var approved = await _context.PointRecords.AsNoTracking()
            .Where(record => record.PeriodId == snapshot.Period.Id && record.Status == PointRecordStatus.Approved)
            .Select(record => new { record.ActivityId, record.StudentId, record.Points })
            .ToListAsync();

        var rows = snapshot.Activities.Select(activity =>
        {
            var registrations = snapshot.Registrations.Where(item => item.ActivityId == activity.Id).ToList();
            var points = approved.Where(item => item.ActivityId == activity.Id && studentIds.Contains(item.StudentId)).ToList();
            var attended = registrations.Count(item => item.Status == RegistrationStatus.Attended);
            return new ActivityReportRowDto
            {
                ActivityId = activity.Id,
                ActivityName = activity.ActivityName,
                OrganizerName = activity.OrganizerName,
                Status = activity.Status,
                EndTime = activity.EndTime,
                TrainingPoints = activity.TrainingPoints,
                RegistrationCount = registrations.Count,
                AttendedCount = attended,
                ParticipationRate = Rate(attended, registrations.Count),
                ApprovedCount = points.Count,
                ApprovedPoints = points.Sum(item => item.Points)
            };
        }).OrderByDescending(row => row.EndTime).ToList();

        return ApiResponseDto<List<ActivityReportRowDto>>.Ok(rows);
    }

    public async Task<ApiResponseDto<PagedResultDto<AtRiskStudentDto>>> GetAtRiskStudentsAsync(ReportQueryDto query, int pageIndex, int pageSize)
    {
        var (error, snapshot) = await LoadSnapshotAsync<PagedResultDto<AtRiskStudentDto>>(query);
        if (error != null)
        {
            return error;
        }

        var target = snapshot!.Period.TargetPoints;
        var atRisk = snapshot.Students
            .Select(student =>
            {
                var points = snapshot.PointsByStudent.GetValueOrDefault(student.Id);
                var registrations = snapshot.Registrations.Where(item => item.StudentId == student.Id).ToList();
                return new AtRiskStudentDto
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    ClassCode = student.ClassCode,
                    AcademicYear = student.AcademicYear,
                    Campus = student.Campus,
                    Points = points,
                    TargetPoints = target,
                    MissingPoints = Math.Max(0, target - points),
                    RegistrationCount = registrations.Count,
                    AttendedCount = registrations.Count(item => item.Status == RegistrationStatus.Attended)
                };
            })
            .Where(student => student.Points < target)
            .OrderByDescending(student => student.MissingPoints)
            .ThenBy(student => student.StudentCode)
            .ToList();

        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return ApiResponseDto<PagedResultDto<AtRiskStudentDto>>.Ok(new PagedResultDto<AtRiskStudentDto>
        {
            Items = atRisk.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = atRisk.Count,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    private sealed record StudentRow(int Id, string StudentCode, string FullName, int? ClassId, string? ClassCode, string AcademicYear, string Campus);

    private sealed record RegistrationRow(int ActivityId, int StudentId, string Status);

    private sealed record Snapshot(
        ReportPeriodDto Period,
        List<StudentRow> Students,
        List<Activity> Activities,
        int CancelledActivityCount,
        List<RegistrationRow> Registrations,
        Dictionary<int, int> PointsByStudent);

    private async Task<(ApiResponseDto<T>? Error, Snapshot? Snapshot)> LoadSnapshotAsync<T>(ReportQueryDto query)
    {
        var period = await ResolvePeriodAsync(query.PeriodId);
        if (period == null)
        {
            return (Fail<T>(query.PeriodId.HasValue ? "Không tìm thấy kỳ đánh giá." : "Chưa có kỳ đánh giá nào để lập báo cáo.",
                StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound), null);
        }

        var studentQuery = _context.Users.AsNoTracking()
            .Where(user => Role.StudentRoles.Contains(user.Role) && user.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Campus))
        {
            studentQuery = studentQuery.Where(user => user.Campus == query.Campus.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.AcademicYear))
        {
            studentQuery = studentQuery.Where(user => user.AcademicYear == query.AcademicYear.Trim());
        }
        if (query.ClassId.HasValue)
        {
            studentQuery = studentQuery.Where(user => user.ClassId == query.ClassId);
        }

        var students = await studentQuery
            .OrderBy(user => user.StudentCode)
            .Select(user => new StudentRow(user.Id, user.StudentCode, user.FullName, user.ClassId,
                user.Class != null ? user.Class.ClassCode : null, user.AcademicYear, user.Campus))
            .ToListAsync();
        var studentIds = students.Select(student => student.Id).ToList();

        var periodActivities = await _context.Activities.AsNoTracking()
            .Where(activity => activity.EndTime >= period.StartDate && activity.EndTime <= period.EndDate &&
                               activity.Status != ActivityStatus.Draft)
            .ToListAsync();
        var activities = periodActivities.Where(activity => CountedStatuses.Contains(activity.Status)).ToList();
        var activityIds = activities.Select(activity => activity.Id).ToList();

        var registrations = await _context.ActivityRegistrations.AsNoTracking()
            .Where(registration => activityIds.Contains(registration.ActivityId) &&
                                   studentIds.Contains(registration.StudentId) &&
                                   registration.Status != RegistrationStatus.Cancelled)
            .Select(registration => new RegistrationRow(registration.ActivityId, registration.StudentId, registration.Status))
            .ToListAsync();

        var points = await _context.PointRecords.AsNoTracking()
            .Where(record => record.PeriodId == period.Id && record.Status == PointRecordStatus.Approved &&
                             studentIds.Contains(record.StudentId))
            .GroupBy(record => record.StudentId)
            .Select(group => new { StudentId = group.Key, Points = group.Sum(record => record.Points) })
            .ToDictionaryAsync(item => item.StudentId, item => item.Points);

        return (null, new Snapshot(
            new ReportPeriodDto
            {
                Id = period.Id,
                Name = period.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                TargetPoints = period.TargetPoints,
                Status = period.Status
            },
            students,
            activities,
            periodActivities.Count(activity => activity.Status == ActivityStatus.Cancelled),
            registrations,
            points));
    }

    /// <summary>The requested period, else the open period containing today, else the most recent one.</summary>
    private async Task<EvaluationPeriod?> ResolvePeriodAsync(int? periodId)
    {
        if (periodId.HasValue)
        {
            return await _context.EvaluationPeriods.AsNoTracking().FirstOrDefaultAsync(period => period.Id == periodId);
        }

        var now = DateTime.UtcNow;
        return await _context.EvaluationPeriods.AsNoTracking()
                   .Where(period => period.Status == EvaluationPeriodStatus.Open && period.StartDate <= now && now <= period.EndDate)
                   .FirstOrDefaultAsync()
               ?? await _context.EvaluationPeriods.AsNoTracking()
                   .OrderByDescending(period => period.StartDate)
                   .FirstOrDefaultAsync();
    }

    private static ReportGroupRowDto BuildGroup(string key, string label, List<StudentRow> students, Snapshot snapshot, int target)
    {
        var ids = students.Select(student => student.Id).ToHashSet();
        var registrations = snapshot.Registrations.Where(item => ids.Contains(item.StudentId)).ToList();
        var attended = registrations.Count(item => item.Status == RegistrationStatus.Attended);
        var points = students.Select(student => snapshot.PointsByStudent.GetValueOrDefault(student.Id)).ToList();
        return new ReportGroupRowDto
        {
            Key = key,
            Label = label,
            StudentCount = students.Count,
            RegistrationCount = registrations.Count,
            AttendedCount = attended,
            ParticipationRate = Rate(attended, registrations.Count),
            ApprovedPoints = points.Sum(),
            AveragePoints = points.Count == 0 ? 0 : Math.Round(points.Average(), 1),
            AtRiskCount = points.Count(value => value < target)
        };
    }

    private static double? Rate(int part, int total) => total == 0 ? null : Math.Round(part * 100.0 / total, 1);

    private static ApiResponseDto<T> Fail<T>(string message, int status, string errorCode) =>
        ApiResponseDto<T>.Fail(message, status, errorCode);
}
