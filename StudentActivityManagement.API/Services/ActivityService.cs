using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed class ActivityService : IActivityService
{
    private static readonly string[] AllowedStatuses =
        ["Draft", "Published", "Ongoing", "Completed", "Cancelled"];

    private readonly AppDbContext _context;
    private readonly IActivityEligibilityService _eligibilityService;

    public ActivityService(AppDbContext context, IActivityEligibilityService eligibilityService)
    {
        _context = context;
        _eligibilityService = eligibilityService;
    }

    public async Task<ApiResponseDto<PagedResultDto<ActivityResponseDto>>> GetActivitiesAsync(
        string? campus, string? status, string? search, int pageIndex = 1, int pageSize = 10, bool upcomingOnly = false)
    {
        var query = BuildActivityQuery(campus, status, search, upcomingOnly);
        return await ToPagedResultAsync(query, pageIndex, pageSize, null);
    }

    public async Task<ApiResponseDto<ActivityResponseDto>> GetActivityByIdAsync(int id)
    {
        var activity = await WithStructure(_context.Activities.AsNoTracking())
            .FirstOrDefaultAsync(item => item.Id == id);
        return activity == null
            ? NotFoundActivity()
            : ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity));
    }

    public async Task<ApiResponseDto<PagedResultDto<ActivityResponseDto>>> GetStudentActivitiesAsync(
        int studentId, string? campus, string? search, int pageIndex = 1, int pageSize = 10)
    {
        var student = await FindStudentAsync(studentId);
        if (student == null)
        {
            return ApiResponseDto<PagedResultDto<ActivityResponseDto>>.Fail(
                "Tài khoản sinh viên không hợp lệ.",
                StatusCodes.Status403Forbidden,
                ApiErrorCodes.AccessDenied);
        }

        var query = BuildActivityQuery(campus, "Published", search, upcomingOnly: true);
        query = _eligibilityService.ApplyEligibleFor(query, student);
        return await ToPagedResultAsync(query, pageIndex, pageSize, student);
    }

    public async Task<ApiResponseDto<ActivityResponseDto>> GetStudentActivityByIdAsync(int id, int studentId)
    {
        var student = await FindStudentAsync(studentId);
        if (student == null)
        {
            return ApiResponseDto<ActivityResponseDto>.Fail(
                "Tài khoản sinh viên không hợp lệ.",
                StatusCodes.Status403Forbidden,
                ApiErrorCodes.AccessDenied);
        }

        var now = DateTime.UtcNow;
        var query = WithStructure(_context.Activities.AsNoTracking())
            .Where(activity => activity.Id == id && activity.Status == "Published" && activity.EndTime >= now);
        var activity = await _eligibilityService.ApplyEligibleFor(query, student).FirstOrDefaultAsync();

        return activity == null
            ? ApiResponseDto<ActivityResponseDto>.Fail(
                "Không tìm thấy hoạt động phù hợp với đối tượng sinh viên.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound)
            : ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity, student));
    }

    public async Task<ApiResponseDto<ActivityResponseDto>> CreateActivityAsync(ActivityCreateDto request)
    {
        var validationError = await ValidateStructureAsync(
            request.Sessions,
            request.AudienceRules,
            request.RegistrationOpenTime,
            request.RegistrationCloseTime,
            request.Status);
        if (validationError != null)
        {
            return InvalidActivity(validationError);
        }

        var orderedSessions = request.Sessions.OrderBy(session => session.SortOrder).ToList();
        var firstSession = orderedSessions[0];
        var activity = new Activity
        {
            ActivityName = request.ActivityName.Trim(),
            Description = request.Description.Trim(),
            OrganizerName = request.OrganizerName.Trim(),
            Location = firstSession.Location.Trim(),
            Campus = firstSession.Campus.Trim(),
            StartTime = orderedSessions.Min(session => session.StartTime),
            EndTime = orderedSessions.Max(session => session.EndTime),
            RegistrationOpenTime = request.RegistrationOpenTime,
            RegistrationCloseTime = request.RegistrationCloseTime,
            MaxParticipants = request.MaxParticipants,
            CurrentParticipantsCount = 0,
            TrainingPoints = request.TrainingPoints,
            TargetAudience = request.TargetAudience.Trim(),
            Status = NormalizeStatus(request.Status)!,
            CreatedAt = DateTime.UtcNow,
            Sessions = request.Sessions.Select(MapSession).ToList(),
            AudienceRules = request.AudienceRules.Select(MapAudienceRule).ToList()
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();
        await LoadStructureAsync(activity);

        return ApiResponseDto<ActivityResponseDto>.Ok(
            MapToResponse(activity),
            "Tạo mới hoạt động rèn luyện thành công.");
    }

    public async Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityAsync(int id, ActivityUpdateDto request)
    {
        var activity = await WithStructure(_context.Activities)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (activity == null)
        {
            return NotFoundActivity();
        }

        var validationError = await ValidateStructureAsync(
            request.Sessions,
            request.AudienceRules,
            request.RegistrationOpenTime,
            request.RegistrationCloseTime,
            request.Status);
        if (validationError != null)
        {
            return InvalidActivity(validationError);
        }
        if (request.MaxParticipants < activity.CurrentParticipantsCount)
        {
            return InvalidActivity("Sức chứa không được nhỏ hơn số sinh viên đã đăng ký.");
        }

        var orderedSessions = request.Sessions.OrderBy(session => session.SortOrder).ToList();
        var firstSession = orderedSessions[0];
        activity.ActivityName = request.ActivityName.Trim();
        activity.Description = request.Description.Trim();
        activity.OrganizerName = request.OrganizerName.Trim();
        activity.Location = firstSession.Location.Trim();
        activity.Campus = firstSession.Campus.Trim();
        activity.StartTime = orderedSessions.Min(session => session.StartTime);
        activity.EndTime = orderedSessions.Max(session => session.EndTime);
        activity.RegistrationOpenTime = request.RegistrationOpenTime;
        activity.RegistrationCloseTime = request.RegistrationCloseTime;
        activity.MaxParticipants = request.MaxParticipants;
        activity.TrainingPoints = request.TrainingPoints;
        activity.TargetAudience = request.TargetAudience.Trim();
        activity.Status = NormalizeStatus(request.Status)!;
        activity.UpdatedAt = DateTime.UtcNow;

        _context.ActivitySessions.RemoveRange(activity.Sessions);
        _context.ActivityAudienceRules.RemoveRange(activity.AudienceRules);
        activity.Sessions = request.Sessions.Select(MapSession).ToList();
        activity.AudienceRules = request.AudienceRules.Select(MapAudienceRule).ToList();

        await _context.SaveChangesAsync();
        await LoadStructureAsync(activity);

        return ApiResponseDto<ActivityResponseDto>.Ok(
            MapToResponse(activity),
            "Cập nhật hoạt động rèn luyện thành công.");
    }

    public async Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityStatusAsync(int id, string newStatus)
    {
        var activity = await WithStructure(_context.Activities)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (activity == null)
        {
            return NotFoundActivity();
        }

        var normalizedStatus = NormalizeStatus(newStatus);
        if (normalizedStatus == null)
        {
            return InvalidActivity("Trạng thái hoạt động không hợp lệ.");
        }

        activity.Status = normalizedStatus;
        activity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ApiResponseDto<ActivityResponseDto>.Ok(
            MapToResponse(activity),
            $"Đã cập nhật trạng thái hoạt động thành '{normalizedStatus}'.");
    }

    public async Task<ApiResponseDto<bool>> DeleteActivityAsync(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
        {
            return ApiResponseDto<bool>.Fail(
                "Không tìm thấy hoạt động.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();
        return ApiResponseDto<bool>.Ok(true, "Xóa hoạt động rèn luyện thành công.");
    }

    public async Task<ApiResponseDto<bool>> RegisterActivityAsync(int activityId, int studentId)
    {
        var now = DateTime.UtcNow;
        var student = await FindStudentAsync(studentId);
        if (student == null)
        {
            return ApiResponseDto<bool>.Fail(
                "Tài khoản sinh viên không hợp lệ.",
                StatusCodes.Status403Forbidden,
                ApiErrorCodes.AccessDenied);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            if (await _context.ActivityRegistrations.AnyAsync(registration =>
                    registration.ActivityId == activityId && registration.StudentId == studentId))
            {
                return ApiResponseDto<bool>.Fail(
                    "Bạn đã đăng ký hoạt động này rồi.",
                    StatusCodes.Status409Conflict,
                    ApiErrorCodes.ResourceConflict);
            }

            var activity = await WithStructure(_context.Activities.AsNoTracking())
                .FirstOrDefaultAsync(item => item.Id == activityId);
            if (activity == null)
            {
                return ApiResponseDto<bool>.Fail(
                    "Không tìm thấy hoạt động.",
                    StatusCodes.Status404NotFound,
                    ApiErrorCodes.ResourceNotFound);
            }

            var eligibility = _eligibilityService.Evaluate(activity, student);
            if (!eligibility.IsEligible)
            {
                return ApiResponseDto<bool>.Fail(
                    eligibility.Reason,
                    StatusCodes.Status403Forbidden,
                    ApiErrorCodes.AccessDenied);
            }

            var updatedRows = await _context.Activities
                .Where(item => item.Id == activityId &&
                               item.Status == "Published" &&
                               item.RegistrationOpenTime <= now &&
                               item.RegistrationCloseTime >= now &&
                               item.CurrentParticipantsCount < item.MaxParticipants)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.CurrentParticipantsCount, item => item.CurrentParticipantsCount + 1));

            if (updatedRows == 0)
            {
                if (activity.Status != "Published")
                {
                    return ApiResponseDto<bool>.Fail(
                        "Hoạt động chưa được công bố.",
                        StatusCodes.Status409Conflict,
                        ApiErrorCodes.BusinessRuleViolation);
                }
                if (now < activity.RegistrationOpenTime || now > activity.RegistrationCloseTime)
                {
                    return ApiResponseDto<bool>.Fail("Không trong thời gian đăng ký.");
                }
                return ApiResponseDto<bool>.Fail(
                    "Hoạt động đã đủ số lượng.",
                    StatusCodes.Status409Conflict,
                    ApiErrorCodes.ResourceConflict);
            }

            _context.ActivityRegistrations.Add(new ActivityRegistration
            {
                ActivityId = activityId,
                StudentId = studentId,
                RegisteredAt = now,
                Status = "Registered"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ApiResponseDto<bool>.Ok(true, "Đăng ký thành công.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ApiResponseDto<bool>.Fail(
                "Không thể đăng ký do dữ liệu đã thay đổi. Vui lòng thử lại.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.ConcurrencyConflict);
        }
    }

    public async Task<ApiResponseDto<bool>> CancelRegistrationAsync(int activityId, int studentId)
    {
        var student = await FindStudentAsync(studentId);
        if (student == null)
        {
            return ApiResponseDto<bool>.Fail(
                "Tài khoản sinh viên không hợp lệ.",
                StatusCodes.Status403Forbidden,
                ApiErrorCodes.AccessDenied);
        }

        var activity = await _context.Activities.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null)
        {
            return ApiResponseDto<bool>.Fail(
                "Không tìm thấy hoạt động.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var registration = await _context.ActivityRegistrations
                .FirstOrDefaultAsync(item => item.ActivityId == activityId && item.StudentId == studentId);

            if (registration == null)
            {
                return ApiResponseDto<bool>.Fail(
                    "Bạn chưa đăng ký tham gia hoạt động này.",
                    StatusCodes.Status404NotFound,
                    ApiErrorCodes.ResourceNotFound);
            }

            if (string.Equals(registration.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponseDto<bool>.Ok(true, "Đăng ký đã được hủy trước đó.");
            }

            if (!string.Equals(registration.Status, "Registered", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponseDto<bool>.Fail(
                    "Chỉ có thể hủy đăng ký khi ở trạng thái đã đăng ký.",
                    StatusCodes.Status409Conflict,
                    ApiErrorCodes.BusinessRuleViolation);
            }

            var now = DateTime.UtcNow;
            if (now > activity.RegistrationCloseTime)
            {
                return ApiResponseDto<bool>.Fail(
                    "Đã hết thời hạn hủy đăng ký hoạt động.",
                    StatusCodes.Status409Conflict,
                    ApiErrorCodes.ResourceConflict);
            }

            var updatedRows = await _context.ActivityRegistrations
                .Where(item => item.Id == registration.Id && item.Status == "Registered")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, "Cancelled"));

            if (updatedRows == 0)
            {
                var recheck = await _context.ActivityRegistrations.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == registration.Id);
                if (recheck != null && string.Equals(recheck.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.CommitAsync();
                    return ApiResponseDto<bool>.Ok(true, "Đăng ký đã được hủy trước đó.");
                }

                return ApiResponseDto<bool>.Fail(
                    "Không thể hủy đăng ký do dữ liệu đã thay đổi. Vui lòng thử lại.",
                    StatusCodes.Status409Conflict,
                    ApiErrorCodes.ConcurrencyConflict);
            }

            var decrementedRows = await _context.Activities
                .Where(item => item.Id == activityId && item.CurrentParticipantsCount > 0)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.CurrentParticipantsCount, item => item.CurrentParticipantsCount - 1));

            if (decrementedRows != 1)
            {
                await transaction.RollbackAsync();
                return ApiResponseDto<bool>.Fail(
                    "Số lượng tham gia không khớp với dữ liệu đăng ký. Vui lòng kiểm tra lại.",
                    StatusCodes.Status409Conflict,
                    ApiErrorCodes.ConcurrencyConflict);
            }

            await transaction.CommitAsync();
            return ApiResponseDto<bool>.Ok(true, "Hủy đăng ký hoạt động thành công.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ApiResponseDto<bool>.Fail(
                "Không thể hủy đăng ký do xung đột dữ liệu. Vui lòng thử lại.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.ConcurrencyConflict);
        }
    }

    private IQueryable<Activity> BuildActivityQuery(string? campus, string? status, string? search, bool upcomingOnly)
    {
        var query = WithStructure(_context.Activities.AsNoTracking());
        if (upcomingOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(activity => activity.EndTime >= now);
        }
        if (!string.IsNullOrWhiteSpace(campus))
        {
            query = query.Where(activity => activity.Sessions.Any(session => session.Campus == campus));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(activity => activity.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(activity =>
                activity.ActivityName.ToLower().Contains(keyword) ||
                activity.OrganizerName.ToLower().Contains(keyword) ||
                activity.Description.ToLower().Contains(keyword) ||
                activity.Sessions.Any(session => session.Location.ToLower().Contains(keyword)));
        }
        return query;
    }

    private async Task<ApiResponseDto<PagedResultDto<ActivityResponseDto>>> ToPagedResultAsync(
        IQueryable<Activity> query, int pageIndex, int pageSize, User? student)
    {
        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync();
        var activities = await query
            .OrderByDescending(activity => activity.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync();

        return ApiResponseDto<PagedResultDto<ActivityResponseDto>>.Ok(new PagedResultDto<ActivityResponseDto>
        {
            Items = activities.Select(activity => MapToResponse(activity, student)).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    private async Task<string?> ValidateStructureAsync(
        IReadOnlyCollection<ActivitySessionInputDto> sessions,
        IReadOnlyCollection<ActivityAudienceRuleInputDto> audienceRules,
        DateTime registrationOpenTime,
        DateTime registrationCloseTime,
        string? status)
    {
        if (NormalizeStatus(status) == null) return "Trạng thái hoạt động không hợp lệ.";
        if (sessions.Count == 0) return "Hoạt động phải có ít nhất một buổi.";
        if (registrationCloseTime <= registrationOpenTime)
            return "Thời gian đóng đăng ký phải sau thời gian mở đăng ký.";
        if (sessions.Any(session => session.EndTime <= session.StartTime))
            return "Thời gian kết thúc của mỗi buổi phải sau thời gian bắt đầu.";
        if (sessions.Select(session => session.SortOrder).Distinct().Count() != sessions.Count)
            return "Thứ tự các buổi không được trùng nhau.";
        if (registrationCloseTime > sessions.Min(session => session.StartTime))
            return "Thời gian đóng đăng ký không được sau buổi đầu tiên.";
        if (audienceRules.Any(rule =>
                !AudienceParticipationType.All.Contains(rule.ParticipationType, StringComparer.OrdinalIgnoreCase)))
            return "Loại tham gia chỉ nhận Required, Recommended hoặc Optional.";
        if (audienceRules.Any(rule =>
                string.IsNullOrWhiteSpace(rule.Campus) &&
                string.IsNullOrWhiteSpace(rule.AcademicYear) &&
                !rule.ClassId.HasValue))
            return "Mỗi quy tắc đối tượng phải có ít nhất Cơ sở, Khóa hoặc Lớp.";

        var requestedClassIds = audienceRules
            .Where(rule => rule.ClassId.HasValue)
            .Select(rule => rule.ClassId!.Value)
            .Distinct()
            .ToList();
        if (requestedClassIds.Count > 0)
        {
            var existingClassCount = await _context.Classes.CountAsync(item => requestedClassIds.Contains(item.Id));
            if (existingClassCount != requestedClassIds.Count)
                return "Một hoặc nhiều lớp trong quy tắc đối tượng không tồn tại.";
        }

        return null;
    }

    private Task<User?> FindStudentAsync(int studentId) => _context.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(user =>
            user.Id == studentId && (user.Role == Role.Student || user.Role == Role.Monitor));

    private static IQueryable<Activity> WithStructure(IQueryable<Activity> query) => query
        .Include(activity => activity.Sessions)
        .Include(activity => activity.AudienceRules)
            .ThenInclude(rule => rule.Class);

    private async Task LoadStructureAsync(Activity activity)
    {
        await _context.Entry(activity).Collection(item => item.Sessions).LoadAsync();
        await _context.Entry(activity).Collection(item => item.AudienceRules).Query()
            .Include(rule => rule.Class)
            .LoadAsync();
    }

    private static ActivitySession MapSession(ActivitySessionInputDto source) => new()
    {
        SessionName = source.SessionName.Trim(),
        Campus = source.Campus.Trim(),
        Location = source.Location.Trim(),
        StartTime = source.StartTime,
        EndTime = source.EndTime,
        SortOrder = source.SortOrder
    };

    private static ActivityAudienceRule MapAudienceRule(ActivityAudienceRuleInputDto source) => new()
    {
        Campus = NormalizeOptional(source.Campus),
        AcademicYear = NormalizeOptional(source.AcademicYear),
        ClassId = source.ClassId,
        ParticipationType = AudienceParticipationType.All.First(value =>
            value.Equals(source.ParticipationType.Trim(), StringComparison.OrdinalIgnoreCase))
    };

    private ActivityResponseDto MapToResponse(Activity activity, User? student = null) => new()
    {
        Id = activity.Id,
        ActivityName = activity.ActivityName,
        Description = activity.Description,
        OrganizerName = activity.OrganizerName,
        Location = activity.Location,
        Campus = activity.Campus,
        StartTime = activity.StartTime,
        EndTime = activity.EndTime,
        RegistrationOpenTime = activity.RegistrationOpenTime,
        RegistrationCloseTime = activity.RegistrationCloseTime,
        MaxParticipants = activity.MaxParticipants,
        CurrentParticipantsCount = activity.CurrentParticipantsCount,
        TrainingPoints = activity.TrainingPoints,
        TargetAudience = activity.TargetAudience,
        Status = activity.Status,
        CreatedAt = activity.CreatedAt,
        Sessions = activity.Sessions.OrderBy(session => session.SortOrder).Select(session => new ActivitySessionResponseDto
        {
            Id = session.Id,
            SessionName = session.SessionName,
            Campus = session.Campus,
            Location = session.Location,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            SortOrder = session.SortOrder
        }).ToList(),
        AudienceRules = activity.AudienceRules.Select(rule => new ActivityAudienceRuleResponseDto
        {
            Id = rule.Id,
            Campus = rule.Campus,
            AcademicYear = rule.AcademicYear,
            ClassId = rule.ClassId,
            ClassCode = rule.Class?.ClassCode,
            ParticipationType = rule.ParticipationType
        }).ToList(),
        Eligibility = student == null ? null : _eligibilityService.Evaluate(activity, student)
    };

    private static ApiResponseDto<ActivityResponseDto> InvalidActivity(string message) =>
        ApiResponseDto<ActivityResponseDto>.Fail(
            message,
            StatusCodes.Status400BadRequest,
            ApiErrorCodes.ValidationFailed);

    private static ApiResponseDto<ActivityResponseDto> NotFoundActivity() =>
        ApiResponseDto<ActivityResponseDto>.Fail(
            "Không tìm thấy hoạt động.",
            StatusCodes.Status404NotFound,
            ApiErrorCodes.ResourceNotFound);

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Published";
        return AllowedStatuses.FirstOrDefault(allowedStatus =>
            allowedStatus.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
