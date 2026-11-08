using System.Data;
using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed class PointAppealService : IPointAppealService
{
    private readonly AppDbContext _context;
    private readonly IScoringService _scoring;
    private readonly int _windowDays;

    public PointAppealService(AppDbContext context, IScoringService scoring, IConfiguration configuration)
    {
        _context = context;
        _scoring = scoring;
        _windowDays = Math.Max(1, configuration.GetValue("PointAppeals:WindowDays", 14));
    }

    public async Task<ApiResponseDto<PointAppealDto>> CreateAsync(int studentId, int pointRecordId, string reason)
    {
        var normalized = reason?.Trim() ?? string.Empty;
        if (normalized.Length is < 10 or > 1000) return Invalid("Lý do khiếu nại phải dài từ 10 đến 1000 ký tự.");
        var record = await _context.PointRecords.AsNoTracking().Include(item => item.Period)
            .FirstOrDefaultAsync(item => item.Id == pointRecordId && item.StudentId == studentId);
        if (record == null) return NotFound();
        if (record.Status != PointRecordStatus.Rejected)
            return Conflict("Chỉ khiếu nại được quyết định điểm bị từ chối.");
        if (record.Period?.Status != EvaluationPeriodStatus.Open)
            return Conflict("Kỳ đánh giá đã khóa.");
        if (DateTime.UtcNow > record.DecidedAt.AddDays(_windowDays))
            return Conflict($"Chỉ được khiếu nại trong {_windowDays} ngày kể từ quyết định điểm.");
        if (await _context.PointAppeals.AnyAsync(item => item.PointRecordId == pointRecordId && item.Status == PointAppealStatus.Pending))
            return Conflict("Bản ghi điểm đã có khiếu nại đang chờ xử lý.");

        var appeal = new PointAppeal
        {
            PointRecordId = pointRecordId, StudentId = studentId, Reason = normalized,
            Status = PointAppealStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        _context.PointAppeals.Add(appeal);
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Conflict("Bản ghi điểm đã có khiếu nại đang chờ xử lý.");
        }
        return ApiResponseDto<PointAppealDto>.Ok(await LoadDtoAsync(appeal.Id), "Đã gửi khiếu nại.");
    }

    public async Task<ApiResponseDto<List<PointAppealDto>>> GetMineAsync(int studentId) =>
        ApiResponseDto<List<PointAppealDto>>.Ok(await Query()
            .Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => Map(item)).ToListAsync());

    public async Task<ApiResponseDto<PointAppealDto>> WithdrawAsync(int studentId, int appealId)
    {
        if (!await _context.PointAppeals.AnyAsync(item => item.Id == appealId && item.StudentId == studentId))
            return NotFound();
        var updated = await _context.PointAppeals
            .Where(item => item.Id == appealId && item.StudentId == studentId && item.Status == PointAppealStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, PointAppealStatus.Withdrawn));
        if (updated == 0) return Conflict("Chỉ rút được khiếu nại đang chờ xử lý.");
        return ApiResponseDto<PointAppealDto>.Ok(await LoadDtoAsync(appealId), "Đã rút khiếu nại.");
    }

    public async Task<ApiResponseDto<PagedResultDto<PointAppealDto>>> GetInboxAsync(
        string? status, int? periodId, int pageIndex, int pageSize)
    {
        var normalized = status?.Trim();
        if (!string.IsNullOrEmpty(normalized) && !PointAppealStatus.All.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return ApiResponseDto<PagedResultDto<PointAppealDto>>.Fail("Trạng thái khiếu nại không hợp lệ.", 400, ApiErrorCodes.ValidationFailed);
        normalized = PointAppealStatus.All.FirstOrDefault(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Query();
        if (normalized != null) query = query.Where(item => item.Status == normalized);
        if (periodId.HasValue) query = query.Where(item => item.PointRecord!.PeriodId == periodId);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).Select(item => Map(item)).ToListAsync();
        return ApiResponseDto<PagedResultDto<PointAppealDto>>.Ok(new PagedResultDto<PointAppealDto>
        {
            Items = items, TotalCount = total, PageIndex = pageIndex, PageSize = pageSize
        });
    }

    public async Task<ApiResponseDto<PointAppealDto>> ResolveAsync(int appealId, bool accept, string response, int staffId)
    {
        var normalized = response?.Trim() ?? string.Empty;
        if (normalized.Length is < 10 or > 1000) return Invalid("Phản hồi phải dài từ 10 đến 1000 ký tự.");
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var appeal = await _context.PointAppeals.AsNoTracking().Include(item => item.PointRecord!)
            .ThenInclude(record => record.Activity).FirstOrDefaultAsync(item => item.Id == appealId);
        if (appeal == null) return NotFound();
        if (appeal.Status != PointAppealStatus.Pending) return Conflict("Khiếu nại đã được xử lý hoặc đã rút.");

        if (accept)
        {
            var prefix = $"Khiếu nại #{appeal.Id} được chấp nhận: ";
            var transitionReason = prefix + normalized[..Math.Min(normalized.Length, 500 - prefix.Length)];
            var pending = await _scoring.TransitionAsync(appeal.PointRecordId,
                new PointRecordTransitionDto { Status = PointRecordStatus.Pending, Reason = transitionReason }, staffId);
            if (!pending.Success) return FromScoring(pending);
            var approved = await _scoring.TransitionAsync(appeal.PointRecordId,
                new PointRecordTransitionDto { Status = PointRecordStatus.Approved, Reason = transitionReason }, staffId);
            if (!approved.Success) return FromScoring(approved);
        }

        var now = DateTime.UtcNow;
        var updated = await _context.PointAppeals.Where(item => item.Id == appealId && item.Status == PointAppealStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, accept ? PointAppealStatus.Accepted : PointAppealStatus.Rejected)
                .SetProperty(item => item.StaffResponse, normalized)
                .SetProperty(item => item.ResolvedById, staffId)
                .SetProperty(item => item.ResolvedAt, now));
        if (updated != 1) return Conflict("Khiếu nại vừa được xử lý bởi người khác.");
        _context.UserNotifications.Add(new UserNotification
        {
            RecipientUserId = appeal.StudentId,
            ActivityId = appeal.PointRecord!.ActivityId,
            SessionId = 0, // UserNotification.SessionId has no foreign key.
            Kind = "PointAppeal",
            SourceKey = $"point-appeal:{appeal.Id}:resolved",
            Title = "Kết quả khiếu nại điểm",
            Body = $"{appeal.PointRecord.Activity?.ActivityName}: {(accept ? "Đã chấp nhận" : "Đã từ chối")}. Phản hồi: {normalized}",
            CreatedAt = now
        });
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ApiResponseDto<PointAppealDto>.Ok(await LoadDtoAsync(appealId), "Đã xử lý khiếu nại.");
    }

    private IQueryable<PointAppeal> Query() => _context.PointAppeals.AsNoTracking()
        .Include(item => item.Student).ThenInclude(student => student!.Class)
        .Include(item => item.PointRecord).ThenInclude(record => record!.Activity);

    private Task<PointAppealDto> LoadDtoAsync(int id) => Query().Where(item => item.Id == id)
        .Select(item => Map(item)).SingleAsync();

    private static PointAppealDto Map(PointAppeal item) => new()
    {
        Id = item.Id, PointRecordId = item.PointRecordId, StudentId = item.StudentId,
        StudentCode = item.Student!.StudentCode, StudentName = item.Student.FullName,
        ClassCode = item.Student.Class != null ? item.Student.Class.ClassCode : null,
        ActivityId = item.PointRecord!.ActivityId,
        ActivityName = item.PointRecord.Activity != null ? item.PointRecord.Activity.ActivityName : string.Empty,
        PeriodId = item.PointRecord.PeriodId, Points = item.PointRecord.Points,
        Reason = item.Reason, Status = item.Status, StaffResponse = item.StaffResponse,
        CreatedAt = item.CreatedAt, ResolvedAt = item.ResolvedAt
    };

    private static ApiResponseDto<PointAppealDto> FromScoring(ApiResponseDto<PointRecordDto> source) =>
        ApiResponseDto<PointAppealDto>.Fail(source.Message, source.HttpStatusCode, source.ErrorCode);
    private static ApiResponseDto<PointAppealDto> NotFound() =>
        ApiResponseDto<PointAppealDto>.Fail("Không tìm thấy khiếu nại hoặc bản ghi điểm.", 404, ApiErrorCodes.ResourceNotFound);
    private static ApiResponseDto<PointAppealDto> Conflict(string message) =>
        ApiResponseDto<PointAppealDto>.Fail(message, 409, ApiErrorCodes.BusinessRuleViolation);
    private static ApiResponseDto<PointAppealDto> Invalid(string message) =>
        ApiResponseDto<PointAppealDto>.Fail(message, 400, ApiErrorCodes.ValidationFailed);
}
