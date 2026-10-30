using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed class ScoringService : IScoringService
{
    private readonly AppDbContext _context;

    public ScoringService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponseDto<ActivityScoringDto>> GetActivityScoringAsync(int activityId)
    {
        var activity = await _context.Activities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return Fail<ActivityScoringDto>("Không tìm thấy hoạt động.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }

        var period = await FindPeriodAsync(activity);
        var candidates = await LoadCandidatesAsync(activityId);
        var periodDto = period == null ? null : new EvaluationPeriodDto
        {
            Id = period.Id,
            Name = period.Name,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            TargetPoints = period.TargetPoints,
            Status = period.Status
        };
        var blockedReason = BlockedReason(activity, period);

        return ApiResponseDto<ActivityScoringDto>.Ok(new ActivityScoringDto
        {
            ActivityId = activity.Id,
            ActivityName = activity.ActivityName,
            ActivityStatus = activity.Status,
            TrainingPoints = activity.TrainingPoints,
            Period = periodDto,
            CanApprove = blockedReason == null,
            BlockedReason = blockedReason,
            EligibleCount = candidates.Count(candidate => candidate.Eligible),
            ApprovedCount = candidates.Count(candidate => candidate.PointRecord?.Status == PointRecordStatus.Approved),
            Candidates = candidates
        });
    }

    public async Task<ApiResponseDto<ApprovePointsResultDto>> ApproveAsync(int activityId, ApprovePointsRequestDto request, int staffId)
    {
        var activity = await _context.Activities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return Fail<ApprovePointsResultDto>("Không tìm thấy hoạt động.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }

        var period = await FindPeriodAsync(activity);
        var blockedReason = BlockedReason(activity, period);
        if (blockedReason != null)
        {
            return Fail<ApprovePointsResultDto>(blockedReason, StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        var candidates = await LoadCandidatesAsync(activityId);
        var requested = request.RegistrationIds is { Count: > 0 }
            ? request.RegistrationIds.Distinct().ToList()
            : candidates.Where(candidate => candidate.Eligible).Select(candidate => candidate.RegistrationId).ToList();
        var result = new ApprovePointsResultDto();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            foreach (var registrationId in requested)
            {
                var candidate = candidates.FirstOrDefault(item => item.RegistrationId == registrationId);
                if (candidate == null)
                {
                    result.Skipped.Add(new SkippedRegistrationDto { RegistrationId = registrationId, Reason = "Không thuộc hoạt động này." });
                    continue;
                }
                if (!candidate.Eligible)
                {
                    result.Skipped.Add(new SkippedRegistrationDto { RegistrationId = registrationId, Reason = candidate.IneligibleReason ?? "Không đủ điều kiện." });
                    continue;
                }

                var record = new PointRecord
                {
                    RegistrationId = registrationId,
                    StudentId = candidate.StudentId,
                    ActivityId = activityId,
                    PeriodId = period!.Id,
                    Points = activity.TrainingPoints,
                    Status = PointRecordStatus.Approved,
                    Reason = note,
                    DecidedById = staffId,
                    DecidedAt = now
                };
                record.History.Add(new PointRecordHistory
                {
                    FromStatus = null,
                    ToStatus = PointRecordStatus.Approved,
                    Points = activity.TrainingPoints,
                    Reason = note,
                    ChangedById = staffId,
                    ChangedAt = now
                });
                _context.PointRecords.Add(record);

                // Kept in sync for older screens; the ledger stays the source of truth.
                await _context.ActivityRegistrations
                    .Where(item => item.Id == registrationId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PointsEarned, activity.TrainingPoints));
                result.ApprovedCount++;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Another approval of the same registration won the race; the unique index kept one decision.
            await transaction.RollbackAsync();
            return Fail<ApprovePointsResultDto>("Một số sinh viên vừa được ghi điểm bởi thao tác khác. Vui lòng tải lại.",
                StatusCodes.Status409Conflict, ApiErrorCodes.ConcurrencyConflict);
        }

        return ApiResponseDto<ApprovePointsResultDto>.Ok(result,
            result.ApprovedCount == 0 ? "Không có sinh viên nào được ghi điểm." : $"Đã ghi điểm cho {result.ApprovedCount} sinh viên.");
    }

    public async Task<ApiResponseDto<List<PointRecordHistoryDto>>> GetHistoryAsync(int pointRecordId)
    {
        if (!await _context.PointRecords.AnyAsync(record => record.Id == pointRecordId))
        {
            return Fail<List<PointRecordHistoryDto>>("Không tìm thấy bản ghi điểm.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }

        var history = await _context.PointRecordHistories.AsNoTracking()
            .Where(item => item.PointRecordId == pointRecordId)
            .OrderBy(item => item.ChangedAt).ThenBy(item => item.Id)
            .Select(item => new PointRecordHistoryDto
            {
                FromStatus = item.FromStatus,
                ToStatus = item.ToStatus,
                Points = item.Points,
                Reason = item.Reason,
                ChangedByName = item.ChangedBy != null ? item.ChangedBy.FullName : null,
                ChangedAt = item.ChangedAt
            })
            .ToListAsync();
        return ApiResponseDto<List<PointRecordHistoryDto>>.Ok(history);
    }

    /// <summary>The period whose range contains the activity's end time.</summary>
    private Task<EvaluationPeriod?> FindPeriodAsync(Activity activity) => _context.EvaluationPeriods.AsNoTracking()
        .FirstOrDefaultAsync(period => period.StartDate <= activity.EndTime && activity.EndTime <= period.EndDate);

    private static string? BlockedReason(Activity activity, EvaluationPeriod? period)
    {
        if (activity.Status != ActivityStatus.Completed)
        {
            return "Chỉ ghi điểm khi hoạt động đã kết thúc (trạng thái Completed).";
        }
        if (period == null)
        {
            return "Chưa có kỳ đánh giá chứa thời gian kết thúc của hoạt động.";
        }
        if (period.Status != EvaluationPeriodStatus.Open)
        {
            return $"Kỳ đánh giá \"{period.Name}\" đã khóa.";
        }

        return null;
    }

    private async Task<List<ScoringCandidateDto>> LoadCandidatesAsync(int activityId)
    {
        var registrations = await _context.ActivityRegistrations.AsNoTracking()
            .Include(item => item.Student!).ThenInclude(student => student.Class)
            .Where(item => item.ActivityId == activityId && item.Status != RegistrationStatus.Cancelled)
            .OrderBy(item => item.Student!.StudentCode)
            .ToListAsync();
        var ids = registrations.Select(item => item.Id).ToList();
        var checkIns = await _context.ActivityCheckIns.AsNoTracking()
            .Where(item => ids.Contains(item.RegistrationId))
            .GroupBy(item => item.RegistrationId)
            .Select(group => new { RegistrationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.RegistrationId, item => item.Count);
        var evidence = await _context.ActivityEvidences.AsNoTracking()
            .Where(item => ids.Contains(item.RegistrationId))
            .GroupBy(item => new { item.RegistrationId, item.ReviewStatus })
            .Select(group => new { group.Key.RegistrationId, group.Key.ReviewStatus, Count = group.Count() })
            .ToListAsync();
        var records = await _context.PointRecords.AsNoTracking()
            .Include(item => item.DecidedBy)
            .Where(item => ids.Contains(item.RegistrationId))
            .ToDictionaryAsync(item => item.RegistrationId);

        return registrations.Select(registration =>
        {
            int EvidenceCount(string status) => evidence
                .Where(item => item.RegistrationId == registration.Id && item.ReviewStatus == status)
                .Sum(item => item.Count);

            var accepted = EvidenceCount(EvidenceReviewStatus.Accepted);
            records.TryGetValue(registration.Id, out var record);
            // Attendance (check-in, code or manual) or an accepted evidence proves participation.
            string? ineligible = record != null
                ? "Đã có quyết định điểm."
                : registration.Status != RegistrationStatus.Attended && accepted == 0
                    ? "Chưa có check-in hoặc minh chứng được duyệt."
                    : null;

            return new ScoringCandidateDto
            {
                RegistrationId = registration.Id,
                StudentId = registration.StudentId,
                StudentCode = registration.Student!.StudentCode,
                FullName = registration.Student.FullName,
                ClassCode = registration.Student.Class?.ClassCode,
                RegistrationStatus = registration.Status,
                CheckInCount = checkIns.GetValueOrDefault(registration.Id),
                AcceptedEvidenceCount = accepted,
                PendingEvidenceCount = EvidenceCount(EvidenceReviewStatus.Pending),
                Eligible = ineligible == null,
                IneligibleReason = ineligible,
                PointRecord = record == null ? null : new PointRecordDto
                {
                    Id = record.Id,
                    RegistrationId = record.RegistrationId,
                    StudentId = record.StudentId,
                    ActivityId = record.ActivityId,
                    PeriodId = record.PeriodId,
                    Points = record.Points,
                    Status = record.Status,
                    Reason = record.Reason,
                    DecidedByName = record.DecidedBy?.FullName,
                    DecidedAt = record.DecidedAt
                }
            };
        }).ToList();
    }

    private static ApiResponseDto<T> Fail<T>(string message, int status, string errorCode) =>
        ApiResponseDto<T>.Fail(message, status, errorCode);
}
