using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Participants;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Storage;

namespace StudentActivityManagement.API.Services;

public sealed class ParticipantService : IParticipantService
{
    private static readonly string[] RegistrationStatuses =
        [RegistrationStatus.Registered, RegistrationStatus.Attended, RegistrationStatus.Cancelled];

    // Attendance can be recorded or corrected while the activity is open, running or just finished
    // (before scoring in US-22); never for drafts or cancelled activities.
    private static readonly string[] AttendanceStatuses =
        [ActivityStatus.Published, ActivityStatus.Ongoing, ActivityStatus.Completed];

    private readonly AppDbContext _context;
    private readonly IEvidenceFileStorage _storage;

    public ParticipantService(AppDbContext context, IEvidenceFileStorage storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<ApiResponseDto<ActivityParticipantsDto>> GetParticipantsAsync(
        int activityId, string? status, string? search, bool? hasPendingEvidence, int pageIndex, int pageSize)
    {
        var activity = await _context.Activities.AsNoTracking()
            .Include(item => item.Sessions)
            .FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return NotFound<ActivityParticipantsDto>("Không tìm thấy hoạt động.");
        }

        string? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            statusFilter = RegistrationStatuses.FirstOrDefault(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (statusFilter == null)
            {
                return Fail<ActivityParticipantsDto>(
                    "Trạng thái đăng ký chỉ nhận Registered, Attended hoặc Cancelled.",
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed);
            }
        }

        var registrations = _context.ActivityRegistrations.AsNoTracking().Where(item => item.ActivityId == activityId);
        var statusCounts = await registrations
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count);
        var pendingEvidence = await _context.ActivityEvidences.CountAsync(evidence =>
            evidence.Registration!.ActivityId == activityId &&
            evidence.Registration.Status != RegistrationStatus.Cancelled &&
            evidence.ReviewStatus == EvidenceReviewStatus.Pending);
        var sessionIds = activity.Sessions.Select(session => session.Id).ToList();
        var sessionCounts = await _context.ActivityCheckIns.AsNoTracking()
            .Where(item => sessionIds.Contains(item.SessionId))
            .GroupBy(item => item.SessionId)
            .Select(group => new { SessionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SessionId, item => item.Count);

        var query = registrations;
        if (statusFilter != null)
        {
            query = query.Where(item => item.Status == statusFilter);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(item =>
                item.Student!.StudentCode.ToLower().Contains(keyword) ||
                item.Student.FullName.ToLower().Contains(keyword));
        }
        if (hasPendingEvidence == true)
        {
            query = query.Where(item => _context.ActivityEvidences.Any(evidence =>
                evidence.RegistrationId == item.Id && evidence.ReviewStatus == EvidenceReviewStatus.Pending));
        }

        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync();
        var page = await query
            .Include(item => item.Student!).ThenInclude(student => student.Class)
            .OrderBy(item => item.Student!.StudentCode)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return ApiResponseDto<ActivityParticipantsDto>.Ok(new ActivityParticipantsDto
        {
            ActivityId = activity.Id,
            ActivityName = activity.ActivityName,
            ActivityStatus = activity.Status,
            Summary = new ParticipantSummaryDto
            {
                Registered = statusCounts.GetValueOrDefault(RegistrationStatus.Registered),
                Attended = statusCounts.GetValueOrDefault(RegistrationStatus.Attended),
                Cancelled = statusCounts.GetValueOrDefault(RegistrationStatus.Cancelled),
                Active = statusCounts.GetValueOrDefault(RegistrationStatus.Registered) + statusCounts.GetValueOrDefault(RegistrationStatus.Attended),
                PendingEvidence = pendingEvidence
            },
            Sessions = activity.Sessions.OrderBy(session => session.SortOrder).Select(session => new SessionAttendanceDto
            {
                SessionId = session.Id,
                SessionName = session.SessionName,
                StartTime = session.StartTime,
                CheckInCount = sessionCounts.GetValueOrDefault(session.Id)
            }).ToList(),
            Participants = new PagedResultDto<ParticipantDto>
            {
                Items = await MapParticipantsAsync(page),
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            }
        });
    }

    public async Task<ApiResponseDto<ParticipantDetailDto>> GetParticipantAsync(int activityId, int registrationId)
    {
        var registration = await FindRegistrationAsync(activityId, registrationId, tracking: false);
        return registration == null
            ? NotFound<ParticipantDetailDto>("Không tìm thấy sinh viên đăng ký trong hoạt động này.")
            : ApiResponseDto<ParticipantDetailDto>.Ok(await MapDetailAsync(registration));
    }

    public async Task<ApiResponseDto<ParticipantDetailDto>> RecordManualCheckInAsync(int activityId, int sessionId, int registrationId, int staffId)
    {
        var (error, registration) = await LoadForAttendanceAsync<ParticipantDetailDto>(activityId, sessionId, registrationId);
        if (error != null)
        {
            return error;
        }
        if (registration!.Status == RegistrationStatus.Cancelled)
        {
            return Conflict<ParticipantDetailDto>("Sinh viên đã hủy đăng ký nên không thể điểm danh.");
        }
        if (await _context.ActivityCheckIns.AnyAsync(item => item.RegistrationId == registrationId && item.SessionId == sessionId))
        {
            return Fail<ParticipantDetailDto>("Sinh viên đã được ghi nhận tham gia buổi này.", StatusCodes.Status409Conflict, ApiErrorCodes.ResourceConflict);
        }

        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Staff may record attendance whatever the activity's method (e.g. a student's phone died),
            // and outside the student check-in window.
            _context.ActivityCheckIns.Add(new ActivityCheckIn
            {
                RegistrationId = registrationId,
                SessionId = sessionId,
                CheckedInAt = now,
                Method = CheckInMethods.Manual,
                RecordedById = staffId
            });
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
            await transaction.RollbackAsync();
            return Fail<ParticipantDetailDto>("Sinh viên đã được ghi nhận tham gia buổi này.", StatusCodes.Status409Conflict, ApiErrorCodes.ResourceConflict);
        }

        return ApiResponseDto<ParticipantDetailDto>.Ok(await MapDetailAsync(registration), "Đã ghi nhận sinh viên tham gia buổi.");
    }

    public async Task<ApiResponseDto<ParticipantDetailDto>> RemoveCheckInAsync(int activityId, int sessionId, int registrationId)
    {
        var (error, registration) = await LoadForAttendanceAsync<ParticipantDetailDto>(activityId, sessionId, registrationId);
        if (error != null)
        {
            return error;
        }

        var checkIn = await _context.ActivityCheckIns
            .FirstOrDefaultAsync(item => item.RegistrationId == registrationId && item.SessionId == sessionId);
        if (checkIn == null)
        {
            return NotFound<ParticipantDetailDto>("Sinh viên chưa có dữ liệu tham gia buổi này.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.ActivityCheckIns.Remove(checkIn);
        var hasOtherCheckIns = await _context.ActivityCheckIns
            .AnyAsync(item => item.RegistrationId == registrationId && item.Id != checkIn.Id);
        if (!hasOtherCheckIns && registration!.Status == RegistrationStatus.Attended)
        {
            registration.Status = RegistrationStatus.Registered;
            registration.CheckInTime = null;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ApiResponseDto<ParticipantDetailDto>.Ok(await MapDetailAsync(registration!), "Đã hủy ghi nhận tham gia buổi.");
    }

    public async Task<ApiResponseDto<StaffEvidenceDto>> ReviewEvidenceAsync(int activityId, int evidenceId, string status, string? note, int staffId)
    {
        var reviewStatus = EvidenceReviewStatus.All.FirstOrDefault(item => item.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (reviewStatus == null)
        {
            return Fail<StaffEvidenceDto>("Kết quả duyệt chỉ nhận Accepted, Rejected hoặc Pending.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (reviewStatus == EvidenceReviewStatus.Rejected && trimmedNote == null)
        {
            return Fail<StaffEvidenceDto>("Từ chối minh chứng cần ghi rõ lý do.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }

        var evidence = await _context.ActivityEvidences
            .Include(item => item.Registration)
            .Include(item => item.Session)
            .FirstOrDefaultAsync(item => item.Id == evidenceId && item.Registration!.ActivityId == activityId);
        if (evidence == null)
        {
            return NotFound<StaffEvidenceDto>("Không tìm thấy minh chứng trong hoạt động này.");
        }
        if (evidence.Registration!.Status == RegistrationStatus.Cancelled)
        {
            return Conflict<StaffEvidenceDto>("Sinh viên đã hủy đăng ký nên không duyệt minh chứng này.");
        }
        if (await HasScoreDecisionAsync(evidence.RegistrationId))
        {
            return Conflict<StaffEvidenceDto>(ScoredMessage);
        }

        var reopened = reviewStatus == EvidenceReviewStatus.Pending;
        evidence.ReviewStatus = reviewStatus;
        evidence.ReviewNote = trimmedNote;
        evidence.ReviewedById = reopened ? null : staffId;
        evidence.ReviewedAt = reopened ? null : DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _context.Entry(evidence).Reference(item => item.ReviewedBy).LoadAsync();

        return ApiResponseDto<StaffEvidenceDto>.Ok(MapEvidence(activityId, evidence), reviewStatus switch
        {
            EvidenceReviewStatus.Accepted => "Đã chấp nhận minh chứng.",
            EvidenceReviewStatus.Rejected => "Đã từ chối minh chứng.",
            _ => "Đã chuyển minh chứng về chờ duyệt."
        });
    }

    public async Task<ApiResponseDto<EvidenceFileDownload>> OpenEvidenceFileAsync(int activityId, int evidenceId)
    {
        var evidence = await _context.ActivityEvidences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == evidenceId && item.Registration!.ActivityId == activityId);
        if (evidence == null)
        {
            return NotFound<EvidenceFileDownload>("Không tìm thấy minh chứng trong hoạt động này.");
        }
        if (evidence.StoredFileName == null)
        {
            return NotFound<EvidenceFileDownload>("Minh chứng này là mã xác nhận, không có file.");
        }

        var stream = await _storage.OpenReadAsync(evidence.StoredFileName);
        return stream == null
            ? NotFound<EvidenceFileDownload>("Không tìm thấy file minh chứng trên máy chủ.")
            : ApiResponseDto<EvidenceFileDownload>.Ok(new EvidenceFileDownload(
                stream,
                evidence.ContentType ?? "application/octet-stream",
                evidence.OriginalFileName ?? $"minh-chung-{evidence.Id}"));
    }

    private async Task<(ApiResponseDto<T>? Error, ActivityRegistration? Registration)> LoadForAttendanceAsync<T>(
        int activityId, int sessionId, int registrationId)
    {
        var activity = await _context.Activities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return (NotFound<T>("Không tìm thấy hoạt động."), null);
        }
        if (!await _context.ActivitySessions.AnyAsync(item => item.Id == sessionId && item.ActivityId == activityId))
        {
            return (NotFound<T>("Không tìm thấy buổi tổ chức của hoạt động này."), null);
        }

        var registration = await FindRegistrationAsync(activityId, registrationId, tracking: true);
        if (registration == null)
        {
            return (NotFound<T>("Không tìm thấy sinh viên đăng ký trong hoạt động này."), null);
        }
        if (!AttendanceStatuses.Contains(activity.Status))
        {
            return (Conflict<T>("Chỉ điểm danh cho hoạt động đang mở, đang diễn ra hoặc vừa kết thúc."), null);
        }
        if (await HasScoreDecisionAsync(registrationId))
        {
            return (Conflict<T>(ScoredMessage), null);
        }

        return (null, registration);
    }

    // Attendance and evidence are the basis of the score; once points are decided (US-22) they are
    // frozen and corrections go through the score-record transitions of US-23.
    private const string ScoredMessage =
        "Đăng ký đã có quyết định điểm rèn luyện nên không thể thay đổi điểm danh hoặc minh chứng.";

    private Task<bool> HasScoreDecisionAsync(int registrationId) =>
        _context.PointRecords.AnyAsync(record => record.RegistrationId == registrationId);

    private Task<ActivityRegistration?> FindRegistrationAsync(int activityId, int registrationId, bool tracking)
    {
        var query = _context.ActivityRegistrations
            .Include(item => item.Student!).ThenInclude(student => student.Class)
            .Where(item => item.Id == registrationId && item.ActivityId == activityId);
        return (tracking ? query : query.AsNoTracking()).FirstOrDefaultAsync();
    }

    private async Task<List<ParticipantDto>> MapParticipantsAsync(List<ActivityRegistration> registrations)
    {
        var ids = registrations.Select(item => item.Id).ToList();
        var checkIns = await LoadCheckInsAsync(ids);
        var evidenceCounts = await _context.ActivityEvidences.AsNoTracking()
            .Where(item => ids.Contains(item.RegistrationId))
            .GroupBy(item => new { item.RegistrationId, item.ReviewStatus })
            .Select(group => new { group.Key.RegistrationId, group.Key.ReviewStatus, Count = group.Count() })
            .ToListAsync();

        return registrations.Select(registration =>
        {
            var participant = new ParticipantDto();
            Fill(participant, registration, checkIns);
            participant.PendingEvidenceCount = Count(EvidenceReviewStatus.Pending);
            participant.AcceptedEvidenceCount = Count(EvidenceReviewStatus.Accepted);
            participant.RejectedEvidenceCount = Count(EvidenceReviewStatus.Rejected);
            return participant;

            int Count(string reviewStatus) => evidenceCounts
                .Where(item => item.RegistrationId == registration.Id && item.ReviewStatus == reviewStatus)
                .Sum(item => item.Count);
        }).ToList();
    }

    private async Task<ParticipantDetailDto> MapDetailAsync(ActivityRegistration registration)
    {
        var checkIns = await LoadCheckInsAsync([registration.Id]);
        var evidences = await _context.ActivityEvidences.AsNoTracking()
            .Include(item => item.Session)
            .Include(item => item.ReviewedBy)
            .Where(item => item.RegistrationId == registration.Id)
            .OrderByDescending(item => item.UploadedAt)
            .ToListAsync();

        var detail = new ParticipantDetailDto
        {
            Evidences = evidences.Select(item => MapEvidence(registration.ActivityId, item)).ToList()
        };
        Fill(detail, registration, checkIns);
        detail.PendingEvidenceCount = evidences.Count(item => item.ReviewStatus == EvidenceReviewStatus.Pending);
        detail.AcceptedEvidenceCount = evidences.Count(item => item.ReviewStatus == EvidenceReviewStatus.Accepted);
        detail.RejectedEvidenceCount = evidences.Count(item => item.ReviewStatus == EvidenceReviewStatus.Rejected);
        return detail;
    }

    private Task<List<ActivityCheckIn>> LoadCheckInsAsync(List<int> registrationIds) => _context.ActivityCheckIns.AsNoTracking()
        .Include(item => item.RecordedBy)
        .Where(item => registrationIds.Contains(item.RegistrationId))
        .ToListAsync();

    private static void Fill(ParticipantDto target, ActivityRegistration registration, List<ActivityCheckIn> checkIns)
    {
        var student = registration.Student!;
        target.RegistrationId = registration.Id;
        target.StudentId = student.Id;
        target.StudentCode = student.StudentCode;
        target.FullName = student.FullName;
        target.ClassCode = student.Class?.ClassCode;
        target.Campus = student.Campus;
        target.AcademicYear = student.AcademicYear;
        target.RegistrationStatus = registration.Status;
        target.RegisteredAt = registration.RegisteredAt;
        target.CheckIns = checkIns
            .Where(item => item.RegistrationId == registration.Id)
            .OrderBy(item => item.CheckedInAt)
            .Select(item => new ParticipantCheckInDto
            {
                SessionId = item.SessionId,
                CheckedInAt = item.CheckedInAt,
                Method = item.Method,
                RecordedByName = item.RecordedBy?.FullName
            }).ToList();
    }

    private static StaffEvidenceDto MapEvidence(int activityId, ActivityEvidence evidence) => new()
    {
        Id = evidence.Id,
        SessionId = evidence.SessionId,
        SessionName = evidence.Session?.SessionName,
        EvidenceType = evidence.EvidenceType,
        ConfirmationCode = evidence.ConfirmationCode,
        Note = evidence.Note,
        OriginalFileName = evidence.OriginalFileName,
        ContentType = evidence.ContentType,
        SizeBytes = evidence.SizeBytes,
        FileUrl = evidence.StoredFileName == null ? null : $"/api/v1/admin/activities/{activityId}/evidences/{evidence.Id}/file",
        ReviewStatus = evidence.ReviewStatus,
        ReviewNote = evidence.ReviewNote,
        ReviewedByName = evidence.ReviewedBy?.FullName,
        ReviewedAt = evidence.ReviewedAt,
        UploadedAt = evidence.UploadedAt
    };

    private static ApiResponseDto<T> NotFound<T>(string message) =>
        Fail<T>(message, StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);

    private static ApiResponseDto<T> Conflict<T>(string message) =>
        Fail<T>(message, StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);

    private static ApiResponseDto<T> Fail<T>(string message, int status, string errorCode) =>
        ApiResponseDto<T>.Fail(message, status, errorCode);
}
