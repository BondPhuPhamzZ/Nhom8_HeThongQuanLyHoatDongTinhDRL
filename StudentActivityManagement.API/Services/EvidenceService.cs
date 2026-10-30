using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Evidence;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Storage;

namespace StudentActivityManagement.API.Services;

public sealed class EvidenceService : IEvidenceService
{
    private readonly AppDbContext _context;
    private readonly IEvidenceFileStorage _fileStorage;
    private readonly IEvidenceFileValidator _fileValidator;
    private readonly IOptions<EvidenceStorageOptions> _options;
    private readonly ILogger<EvidenceService> _logger;

    public EvidenceService(
        AppDbContext context,
        IEvidenceFileStorage fileStorage,
        IEvidenceFileValidator fileValidator,
        IOptions<EvidenceStorageOptions> options,
        ILogger<EvidenceService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _fileValidator = fileValidator;
        _options = options;
        _logger = logger;
    }

    public async Task<ApiResponseDto<List<EvidenceResponseDto>>> GetEvidencesAsync(
        int activityId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var activity = await _context.Activities.AsNoTracking()
            .Include(a => a.Sessions)
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);
        if (activity == null)
        {
            return ApiResponseDto<List<EvidenceResponseDto>>.Fail(
                "Không tìm thấy hoạt động.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        var registration = await _context.ActivityRegistrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ActivityId == activityId && r.StudentId == studentId, cancellationToken);
        if (registration == null)
        {
            return ApiResponseDto<List<EvidenceResponseDto>>.Fail(
                "Bạn chưa đăng ký tham gia hoạt động này.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        var deadline = CalculateDeadline(activity);
        var now = DateTime.UtcNow;

        var evidences = await _context.ActivityEvidences.AsNoTracking()
            .Include(e => e.Session)
            .Where(e => e.RegistrationId == registration.Id)
            .OrderByDescending(e => e.UploadedAt)
            .ToListAsync(cancellationToken);

        var dtos = evidences.Select(e => new EvidenceResponseDto
        {
            Id = e.Id,
            ActivityId = activityId,
            SessionId = e.SessionId,
            SessionName = e.Session?.SessionName,
            EvidenceType = e.EvidenceType,
            ConfirmationCode = e.ConfirmationCode,
            Note = e.Note,
            OriginalFileName = e.OriginalFileName,
            ContentType = e.ContentType,
            SizeBytes = e.SizeBytes,
            FileUrl = e.StoredFileName != null
                ? $"/api/v1/student-activities/{activityId}/evidences/{e.Id}/file"
                : null,
            ReviewStatus = e.ReviewStatus,
            ReviewNote = e.ReviewNote,
            UploadedAt = e.UploadedAt,
            CanDelete = e.ReviewStatus == EvidenceReviewStatus.Pending && now <= deadline
        }).ToList();

        return ApiResponseDto<List<EvidenceResponseDto>>.Ok(dtos);
    }

    public async Task<ApiResponseDto<EvidenceResponseDto>> UploadAsync(
        int activityId,
        int studentId,
        EvidenceUploadDto dto,
        CancellationToken cancellationToken = default)
    {
        // 1. Hoạt động tồn tại
        var activity = await _context.Activities
            .Include(a => a.Sessions)
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);
        if (activity == null)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                "Không tìm thấy hoạt động.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        // 2. Có đăng ký của user
        var registration = await _context.ActivityRegistrations
            .FirstOrDefaultAsync(r => r.ActivityId == activityId && r.StudentId == studentId, cancellationToken);
        if (registration == null)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                "Bạn chưa đăng ký tham gia hoạt động này.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        // 3. Đăng ký không Cancelled
        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                "Đăng ký của bạn đã bị hủy nên không thể nộp minh chứng.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.BusinessRuleViolation);
        }

        // 4. Trạng thái hoạt động và hạn nộp
        if (activity.Status == ActivityStatus.Published)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                "Hoạt động chưa diễn ra nên chưa thể nộp minh chứng.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.BusinessRuleViolation);
        }

        if (activity.Status != ActivityStatus.Ongoing && activity.Status != ActivityStatus.Completed)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                "Trạng thái hoạt động không cho phép nộp minh chứng.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.BusinessRuleViolation);
        }

        var deadline = CalculateDeadline(activity);
        if (DateTime.UtcNow > deadline)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                "Đã hết hạn nộp minh chứng cho hoạt động này.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.BusinessRuleViolation);
        }

        // 5. Validate loại, file hoặc mã, sessionId
        var normalizedType = EvidenceTypes.Normalize(dto.EvidenceType);
        if (normalizedType == null)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                "Loại minh chứng không hợp lệ. Chỉ chấp nhận Image, Document hoặc ConfirmationCode.",
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed);
        }

        EvidenceFileCheck? check = null;
        if (normalizedType == EvidenceTypes.ConfirmationCode)
        {
            if (dto.File != null && dto.File.Length > 0)
            {
                return ApiResponseDto<EvidenceResponseDto>.Fail(
                    "Minh chứng mã xác nhận không được đính kèm file.",
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed);
            }

            if (string.IsNullOrWhiteSpace(dto.ConfirmationCode) ||
                dto.ConfirmationCode.Trim().Length < 4 ||
                dto.ConfirmationCode.Trim().Length > 100)
            {
                return ApiResponseDto<EvidenceResponseDto>.Fail(
                    "Mã xác nhận phải từ 4 đến 100 ký tự.",
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed);
            }
        }
        else
        {
            if (dto.File == null || dto.File.Length == 0)
            {
                return ApiResponseDto<EvidenceResponseDto>.Fail(
                    "Vui lòng chọn file minh chứng.",
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed);
            }

            if (!string.IsNullOrWhiteSpace(dto.ConfirmationCode))
            {
                return ApiResponseDto<EvidenceResponseDto>.Fail(
                    "Minh chứng dạng file không được kèm mã xác nhận.",
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed);
            }

            check = _fileValidator.Validate(dto.File, normalizedType);
            if (!check.IsValid)
            {
                return ApiResponseDto<EvidenceResponseDto>.Fail(
                    check.ErrorMessage ?? "File minh chứng không hợp lệ.",
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed);
            }
        }

        ActivitySession? selectedSession = null;
        if (dto.SessionId.HasValue)
        {
            selectedSession = activity.Sessions.FirstOrDefault(s => s.Id == dto.SessionId.Value);
            if (selectedSession == null)
            {
                return ApiResponseDto<EvidenceResponseDto>.Fail(
                    "Buổi hoạt động không thuộc hoạt động này.",
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed);
            }
        }

        if (dto.Note != null && dto.Note.Length > 500)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                "Ghi chú không được vượt quá 500 ký tự.",
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed);
        }

        // 6. Đếm số minh chứng hiện có < giới hạn
        var currentCount = await _context.ActivityEvidences
            .CountAsync(e => e.RegistrationId == registration.Id, cancellationToken);
        if (currentCount >= _options.Value.MaxEvidencesPerRegistration)
        {
            return ApiResponseDto<EvidenceResponseDto>.Fail(
                $"Mỗi đăng ký chỉ được nộp tối đa {_options.Value.MaxEvidencesPerRegistration} minh chứng.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.BusinessRuleViolation);
        }

        // 7. Lưu file
        string? storedFileName = null;
        if (check != null && dto.File != null)
        {
            using var stream = dto.File.OpenReadStream();
            storedFileName = await _fileStorage.SaveAsync(stream, check.NormalizedExtension!, cancellationToken);
        }

        // 8. Lưu DB trong transaction
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var countInTx = await _context.ActivityEvidences
                .CountAsync(e => e.RegistrationId == registration.Id, cancellationToken);
            if (countInTx >= _options.Value.MaxEvidencesPerRegistration)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (storedFileName != null)
                {
                    try
                    {
                        await _fileStorage.DeleteAsync(storedFileName, CancellationToken.None);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Không thể xóa file {StoredFileName} khi vượt giới hạn.", storedFileName);
                    }
                }
                return ApiResponseDto<EvidenceResponseDto>.Fail(
                    $"Mỗi đăng ký chỉ được nộp tối đa {_options.Value.MaxEvidencesPerRegistration} minh chứng.",
                    StatusCodes.Status409Conflict,
                    ApiErrorCodes.BusinessRuleViolation);
            }

            var evidence = new ActivityEvidence
            {
                RegistrationId = registration.Id,
                SessionId = dto.SessionId,
                EvidenceType = normalizedType,
                ConfirmationCode = normalizedType == EvidenceTypes.ConfirmationCode ? dto.ConfirmationCode?.Trim() : null,
                Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                OriginalFileName = dto.File != null ? Path.GetFileName(dto.File.FileName) : null,
                StoredFileName = storedFileName,
                ContentType = check?.ContentType,
                SizeBytes = dto.File?.Length,
                ReviewStatus = EvidenceReviewStatus.Pending,
                UploadedAt = DateTime.UtcNow
            };

            _context.ActivityEvidences.Add(evidence);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new EvidenceResponseDto
            {
                Id = evidence.Id,
                ActivityId = activityId,
                SessionId = evidence.SessionId,
                SessionName = selectedSession?.SessionName,
                EvidenceType = evidence.EvidenceType,
                ConfirmationCode = evidence.ConfirmationCode,
                Note = evidence.Note,
                OriginalFileName = evidence.OriginalFileName,
                ContentType = evidence.ContentType,
                SizeBytes = evidence.SizeBytes,
                FileUrl = evidence.StoredFileName != null
                    ? $"/api/v1/student-activities/{activityId}/evidences/{evidence.Id}/file"
                    : null,
                ReviewStatus = evidence.ReviewStatus,
                ReviewNote = evidence.ReviewNote,
                UploadedAt = evidence.UploadedAt,
                CanDelete = true
            };

            return ApiResponseDto<EvidenceResponseDto>.Ok(response, "Nộp minh chứng thành công.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (storedFileName != null)
            {
                try
                {
                    await _fileStorage.DeleteAsync(storedFileName, CancellationToken.None);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Lỗi khi xóa file tạm {StoredFileName} sau khi lưu DB thất bại.", storedFileName);
                }
            }
            _logger.LogError(ex, "Lỗi khi lưu minh chứng cho hoạt động {ActivityId}, sinh viên {StudentId}.", activityId, studentId);
            throw;
        }
    }

    public async Task<ApiResponseDto<EvidenceFileDownload>> OpenFileAsync(
        int activityId,
        int evidenceId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var activity = await _context.Activities.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);
        if (activity == null)
        {
            return ApiResponseDto<EvidenceFileDownload>.Fail(
                "Không tìm thấy hoạt động.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        var registration = await _context.ActivityRegistrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ActivityId == activityId && r.StudentId == studentId, cancellationToken);
        if (registration == null)
        {
            return ApiResponseDto<EvidenceFileDownload>.Fail(
                "Bạn chưa đăng ký tham gia hoạt động này.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        var evidence = await _context.ActivityEvidences.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == evidenceId && e.RegistrationId == registration.Id, cancellationToken);
        if (evidence == null)
        {
            return ApiResponseDto<EvidenceFileDownload>.Fail(
                "Không tìm thấy minh chứng.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        if (evidence.StoredFileName == null)
        {
            return ApiResponseDto<EvidenceFileDownload>.Fail(
                "Minh chứng này là mã xác nhận, không có file.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        var stream = await _fileStorage.OpenReadAsync(evidence.StoredFileName, cancellationToken);
        if (stream == null)
        {
            return ApiResponseDto<EvidenceFileDownload>.Fail(
                "Không tìm thấy file minh chứng trên máy chủ.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        return ApiResponseDto<EvidenceFileDownload>.Ok(new EvidenceFileDownload(
            stream,
            evidence.ContentType ?? "application/octet-stream",
            evidence.OriginalFileName ?? $"minh-chung-{evidence.Id}"));
    }

    public async Task<ApiResponseDto<object>> DeleteAsync(
        int activityId,
        int evidenceId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var activity = await _context.Activities
            .Include(a => a.Sessions)
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);
        if (activity == null)
        {
            return ApiResponseDto<object>.Fail(
                "Không tìm thấy hoạt động.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        var registration = await _context.ActivityRegistrations
            .FirstOrDefaultAsync(r => r.ActivityId == activityId && r.StudentId == studentId, cancellationToken);
        if (registration == null)
        {
            return ApiResponseDto<object>.Fail(
                "Bạn chưa đăng ký tham gia hoạt động này.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        var evidence = await _context.ActivityEvidences
            .FirstOrDefaultAsync(e => e.Id == evidenceId && e.RegistrationId == registration.Id, cancellationToken);
        if (evidence == null)
        {
            return ApiResponseDto<object>.Fail(
                "Không tìm thấy minh chứng.",
                StatusCodes.Status404NotFound,
                ApiErrorCodes.ResourceNotFound);
        }

        if (evidence.ReviewStatus != EvidenceReviewStatus.Pending)
        {
            return ApiResponseDto<object>.Fail(
                "Không thể xóa minh chứng đã được duyệt.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.BusinessRuleViolation);
        }

        var deadline = CalculateDeadline(activity);
        if (DateTime.UtcNow > deadline)
        {
            return ApiResponseDto<object>.Fail(
                "Đã hết hạn nộp nên không thể xóa minh chứng.",
                StatusCodes.Status409Conflict,
                ApiErrorCodes.BusinessRuleViolation);
        }

        var storedFileName = evidence.StoredFileName;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.ActivityEvidences.Remove(evidence);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Lỗi khi xóa minh chứng {EvidenceId}.", evidenceId);
            throw;
        }

        if (storedFileName != null)
        {
            try
            {
                await _fileStorage.DeleteAsync(storedFileName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể xóa file {StoredFileName} của minh chứng {EvidenceId}.", storedFileName, evidenceId);
            }
        }

        return ApiResponseDto<object>.Ok(new object(), "Đã xóa minh chứng thành công.");
    }

    private DateTime CalculateDeadline(Activity activity)
    {
        var lastEndTime = activity.Sessions != null && activity.Sessions.Count > 0
            ? activity.Sessions.Max(s => s.EndTime)
            : activity.EndTime;
        return lastEndTime.AddDays(_options.Value.SubmissionWindowDays);
    }
}
