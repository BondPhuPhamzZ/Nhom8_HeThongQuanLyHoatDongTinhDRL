using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.ClassSpace;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed class ClassSpaceService : IClassSpaceService
{
    private const int PreviewLength = 200;
    private readonly AppDbContext _context;

    public ClassSpaceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponseDto<ClassSpaceDto>> GetThreadsAsync(int userId, string? kind, string? search, int pageIndex, int pageSize)
    {
        var (error, member) = await ResolveMemberAsync<ClassSpaceDto>(userId);
        if (error != null)
        {
            return error;
        }

        var query = _context.ClassThreads.AsNoTracking().Where(thread => thread.ClassId == member!.ClassId);
        if (!string.IsNullOrWhiteSpace(kind))
        {
            var normalizedKind = ClassThreadKind.All.FirstOrDefault(item => item.Equals(kind.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalizedKind == null)
            {
                return Fail<ClassSpaceDto>("Loại bài chỉ nhận Question, Discussion hoặc Announcement.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
            }
            query = query.Where(thread => thread.Kind == normalizedKind);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(thread => thread.Title.ToLower().Contains(keyword) || thread.Body.ToLower().Contains(keyword));
        }

        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var totalCount = await query.CountAsync();
        var threads = await query
            .Include(thread => thread.Author)
            .OrderByDescending(thread => thread.IsPinned)
            .ThenByDescending(thread => thread.LastActivityAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(thread => new
            {
                Thread = thread,
                ReplyCount = thread.Replies.Count,
                EscalationStatus = thread.Escalations.OrderByDescending(escalation => escalation.Id).Select(escalation => escalation.Status).FirstOrDefault()
            })
            .ToListAsync();

        return ApiResponseDto<ClassSpaceDto>.Ok(new ClassSpaceDto
        {
            ClassId = member!.ClassId,
            ClassCode = member.ClassCode,
            ClassName = member.ClassName,
            MonitorName = member.MonitorName,
            IsMonitor = member.MonitorId == userId,
            Threads = new PagedResultDto<ClassThreadSummaryDto>
            {
                Items = threads.Select(item => FillSummary(new ClassThreadSummaryDto(), item.Thread, item.ReplyCount, item.EscalationStatus, userId)).ToList(),
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            }
        });
    }

    public async Task<ApiResponseDto<ClassThreadDetailDto>> GetThreadAsync(int userId, int threadId)
    {
        var (error, member) = await ResolveMemberAsync<ClassThreadDetailDto>(userId);
        if (error != null)
        {
            return error;
        }

        var detail = await LoadDetailAsync(member!, threadId, userId);
        return detail == null ? ThreadNotFound<ClassThreadDetailDto>() : ApiResponseDto<ClassThreadDetailDto>.Ok(detail);
    }

    public async Task<ApiResponseDto<ClassThreadDetailDto>> CreateThreadAsync(int userId, ClassThreadCreateDto request)
    {
        var (error, member) = await ResolveMemberAsync<ClassThreadDetailDto>(userId);
        if (error != null)
        {
            return error;
        }

        var requestedKind = ClassThreadKind.All.FirstOrDefault(item => item.Equals(request.Kind?.Trim(), StringComparison.OrdinalIgnoreCase));
        var kind = requestedKind == ClassThreadKind.Announcement && member!.MonitorId == userId
            ? requestedKind
            : ClassThreadKind.MemberKinds.FirstOrDefault(item => item.Equals(request.Kind?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (kind == null)
        {
            var failureMessage = requestedKind == ClassThreadKind.Announcement
                ? "Chỉ lớp trưởng hiện tại mới được đăng thông báo."
                : "Loại bài chỉ nhận Question (câu hỏi), Discussion (thảo luận) hoặc Announcement (thông báo của lớp trưởng).";
            var code = requestedKind == ClassThreadKind.Announcement ? ApiErrorCodes.AccessDenied : ApiErrorCodes.ValidationFailed;
            var status = requestedKind == ClassThreadKind.Announcement ? StatusCodes.Status403Forbidden : StatusCodes.Status400BadRequest;
            return Fail<ClassThreadDetailDto>(failureMessage, status, code);
        }

        var title = request.Title.Trim();
        var body = request.Body.Trim();
        if (title.Length < 3 || body.Length == 0)
        {
            return Fail<ClassThreadDetailDto>("Tiêu đề tối thiểu 3 ký tự và nội dung không được để trống.",
                StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }

        var now = DateTime.UtcNow;
        var thread = new ClassThread
        {
            ClassId = member!.ClassId,
            AuthorId = userId,
            Kind = kind,
            Title = title,
            Body = body,
            Status = ClassThreadStatus.Open,
            IsPinned = kind == ClassThreadKind.Announcement,
            CreatedAt = now,
            LastActivityAt = now
        };
        _context.ClassThreads.Add(thread);
        await _context.SaveChangesAsync();

        var detail = await LoadDetailAsync(member, thread.Id, userId);
        var message = kind switch
        {
            ClassThreadKind.Question => "Đã gửi câu hỏi cho lớp.",
            ClassThreadKind.Announcement => "Đã đăng và ghim thông báo cho lớp.",
            _ => "Đã mở chủ đề thảo luận."
        };
        return ApiResponseDto<ClassThreadDetailDto>.Ok(detail!, message);
    }

    public async Task<ApiResponseDto<ClassThreadDetailDto>> SetPinnedAsync(int userId, int threadId, ClassThreadPinDto request)
    {
        var (error, member) = await ResolveMemberAsync<ClassThreadDetailDto>(userId);
        if (error != null) return error;
        if (member!.MonitorId != userId)
        {
            return Fail<ClassThreadDetailDto>("Chỉ lớp trưởng hiện tại mới được ghim hoặc bỏ ghim chủ đề.",
                StatusCodes.Status403Forbidden, ApiErrorCodes.AccessDenied);
        }

        var thread = await _context.ClassThreads.FirstOrDefaultAsync(item => item.Id == threadId && item.ClassId == member.ClassId);
        if (thread == null) return ThreadNotFound<ClassThreadDetailDto>();
        thread.IsPinned = request.IsPinned;
        await _context.SaveChangesAsync();
        var detail = await LoadDetailAsync(member, threadId, userId);
        return ApiResponseDto<ClassThreadDetailDto>.Ok(detail!, request.IsPinned ? "Đã ghim chủ đề lên đầu lớp." : "Đã bỏ ghim chủ đề.");
    }

    public async Task<ApiResponseDto<ClassThreadReplyDto>> ReplyAsync(int userId, int threadId, ClassThreadReplyCreateDto request)
    {
        var (error, member) = await ResolveMemberAsync<ClassThreadReplyDto>(userId);
        if (error != null)
        {
            return error;
        }

        var thread = await _context.ClassThreads.FirstOrDefaultAsync(item => item.Id == threadId && item.ClassId == member!.ClassId);
        if (thread == null)
        {
            return ThreadNotFound<ClassThreadReplyDto>();
        }
        if (thread.Status == ClassThreadStatus.Closed)
        {
            return Fail<ClassThreadReplyDto>("Chủ đề đã đóng, không nhận thêm trả lời.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            return Fail<ClassThreadReplyDto>("Nội dung trả lời không được để trống.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }

        var now = DateTime.UtcNow;
        var reply = new ClassThreadReply { ThreadId = threadId, AuthorId = userId, Body = body, CreatedAt = now };
        _context.ClassThreadReplies.Add(reply);
        thread.LastActivityAt = now;
        await _context.SaveChangesAsync();

        var author = await _context.Users.AsNoTracking().FirstAsync(user => user.Id == userId);
        return ApiResponseDto<ClassThreadReplyDto>.Ok(MapReply(reply, author, userId), "Đã gửi trả lời.");
    }

    public async Task<ApiResponseDto<ClassThreadDetailDto>> MarkAnswerAsync(int userId, int threadId, int replyId)
    {
        var (error, member) = await ResolveMemberAsync<ClassThreadDetailDto>(userId);
        if (error != null) return error;
        if (member!.MonitorId != userId)
        {
            return Fail<ClassThreadDetailDto>("Chỉ lớp trưởng hiện tại mới được chọn câu trả lời.",
                StatusCodes.Status403Forbidden, ApiErrorCodes.AccessDenied);
        }

        var thread = await _context.ClassThreads.FirstOrDefaultAsync(item => item.Id == threadId && item.ClassId == member.ClassId);
        if (thread == null) return ThreadNotFound<ClassThreadDetailDto>();
        if (thread.Kind != ClassThreadKind.Question || thread.Status == ClassThreadStatus.Closed)
        {
            return Fail<ClassThreadDetailDto>("Chỉ chọn câu trả lời cho câu hỏi chưa đóng.",
                StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        var replies = await _context.ClassThreadReplies.Where(reply => reply.ThreadId == threadId).ToListAsync();
        var selected = replies.FirstOrDefault(reply => reply.Id == replyId);
        if (selected == null)
        {
            return Fail<ClassThreadDetailDto>("Không tìm thấy trả lời trong câu hỏi này.",
                StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }
        if (!selected.IsAnswer)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            foreach (var reply in replies) reply.IsAnswer = reply.Id == replyId;
            thread.Status = ClassThreadStatus.Answered;
            thread.LastActivityAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var detail = await LoadDetailAsync(member, threadId, userId);
        return ApiResponseDto<ClassThreadDetailDto>.Ok(detail!, "Đã chọn câu trả lời và cập nhật trạng thái câu hỏi.");
    }

    public async Task<ApiResponseDto<bool>> DeleteThreadAsync(int userId, int threadId)
    {
        var (error, member) = await ResolveMemberAsync<bool>(userId);
        if (error != null)
        {
            return error;
        }

        var thread = await _context.ClassThreads.FirstOrDefaultAsync(item => item.Id == threadId && item.ClassId == member!.ClassId);
        if (thread == null)
        {
            return ThreadNotFound<bool>();
        }
        if (thread.AuthorId != userId)
        {
            return Fail<bool>("Chỉ người tạo mới xóa được chủ đề.", StatusCodes.Status403Forbidden, ApiErrorCodes.AccessDenied);
        }
        // Once classmates have answered, the thread is part of the class's record and stays.
        if (await _context.ClassThreadReplies.AnyAsync(reply => reply.ThreadId == threadId && reply.AuthorId != userId))
        {
            return Fail<bool>("Chủ đề đã có người khác trả lời nên không thể xóa.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }
        // Raised threads stay so Phòng QL HSSV never loses the question it has to answer.
        if (await _context.ThreadEscalations.AnyAsync(escalation => escalation.ThreadId == threadId))
        {
            return Fail<bool>("Chủ đề đã được lớp trưởng chuyển lên Phòng QL HSSV nên không thể xóa.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        _context.ClassThreads.Remove(thread);
        await _context.SaveChangesAsync();
        return ApiResponseDto<bool>.Ok(true, "Đã xóa chủ đề.");
    }

    public async Task<ApiResponseDto<bool>> DeleteReplyAsync(int userId, int threadId, int replyId)
    {
        var (error, member) = await ResolveMemberAsync<bool>(userId);
        if (error != null)
        {
            return error;
        }

        var reply = await _context.ClassThreadReplies
            .FirstOrDefaultAsync(item => item.Id == replyId && item.ThreadId == threadId && item.Thread!.ClassId == member!.ClassId);
        if (reply == null)
        {
            return Fail<bool>("Không tìm thấy trả lời trong lớp của bạn.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }
        if (reply.AuthorId != userId)
        {
            return Fail<bool>("Chỉ người viết mới xóa được trả lời.", StatusCodes.Status403Forbidden, ApiErrorCodes.AccessDenied);
        }
        if (reply.IsAnswer)
        {
            return Fail<bool>("Trả lời đã được lớp trưởng chọn làm câu trả lời nên không thể xóa.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        _context.ClassThreadReplies.Remove(reply);
        await _context.SaveChangesAsync();
        return ApiResponseDto<bool>.Ok(true, "Đã xóa trả lời.");
    }

    public async Task<ApiResponseDto<ThreadEscalationDto>> EscalateAsync(int userId, int threadId, ThreadEscalationCreateDto request)
    {
        var (error, member) = await ResolveMonitorAsync<ThreadEscalationDto>(userId);
        if (error != null)
        {
            return error;
        }

        var thread = await _context.ClassThreads.AsNoTracking().FirstOrDefaultAsync(item => item.Id == threadId && item.ClassId == member!.ClassId);
        if (thread == null)
        {
            return ThreadNotFound<ThreadEscalationDto>();
        }
        if (!ClassThreadKind.MemberKinds.Contains(thread.Kind))
        {
            return Fail<ThreadEscalationDto>("Chỉ chuyển lên được câu hỏi hoặc chủ đề thảo luận.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        var question = request.Question.Trim();
        if (question.Length < 5)
        {
            return Fail<ThreadEscalationDto>("Nội dung cần xác nhận tối thiểu 5 ký tự.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }
        if (await _context.ThreadEscalations.AnyAsync(item => item.ThreadId == threadId && item.Status == EscalationStatus.Pending))
        {
            return PendingExists();
        }

        var escalation = new ThreadEscalation
        {
            ThreadId = threadId,
            RaisedById = userId,
            Question = question,
            Status = EscalationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _context.ThreadEscalations.Add(escalation);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another request raised the same thread first; the filtered unique index rejected this one.
            return PendingExists();
        }

        return ApiResponseDto<ThreadEscalationDto>.Ok((await LoadEscalationAsync(escalation.Id))!, "Đã chuyển câu hỏi lên Phòng QL HSSV.");

        static ApiResponseDto<ThreadEscalationDto> PendingExists() => Fail<ThreadEscalationDto>(
            "Chủ đề đang chờ Phòng QL HSSV trả lời, không chuyển lên lần nữa.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
    }

    public async Task<ApiResponseDto<PagedResultDto<ThreadEscalationDto>>> GetEscalationsAsync(int userId, string? status, int pageIndex, int pageSize)
    {
        var (error, member) = await ResolveMonitorAsync<PagedResultDto<ThreadEscalationDto>>(userId);
        if (error != null)
        {
            return error;
        }

        var query = EscalationQuery().Where(escalation => escalation.Thread!.ClassId == member!.ClassId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = EscalationStatus.All.FirstOrDefault(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalized == null)
            {
                return Fail<PagedResultDto<ThreadEscalationDto>>("Trạng thái chỉ nhận Pending, Answered hoặc Withdrawn.",
                    StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
            }
            query = query.Where(escalation => escalation.Status == normalized);
        }

        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(escalation => escalation.CreatedAt)
            .ThenByDescending(escalation => escalation.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return ApiResponseDto<PagedResultDto<ThreadEscalationDto>>.Ok(new PagedResultDto<ThreadEscalationDto>
        {
            Items = items.Select(MapEscalation).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponseDto<ThreadEscalationDto>> WithdrawEscalationAsync(int userId, int escalationId)
    {
        var (error, member) = await ResolveMonitorAsync<ThreadEscalationDto>(userId);
        if (error != null)
        {
            return error;
        }

        var escalation = await _context.ThreadEscalations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == escalationId && item.Thread!.ClassId == member!.ClassId);
        if (escalation == null)
        {
            return Fail<ThreadEscalationDto>("Không tìm thấy câu hỏi đã chuyển của lớp bạn.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }
        if (!EscalationStatus.NextOf(escalation.Status).Contains(EscalationStatus.Withdrawn))
        {
            return Fail<ThreadEscalationDto>("Chỉ rút lại được câu hỏi đang chờ Phòng QL HSSV trả lời.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        // Answer and withdrawal race for the same Pending row; only one may win.
        var updated = await _context.ThreadEscalations
            .Where(item => item.Id == escalationId && item.Status == EscalationStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, EscalationStatus.Withdrawn)
                .SetProperty(item => item.WithdrawnAt, DateTime.UtcNow));
        if (updated == 0)
        {
            return Fail<ThreadEscalationDto>("Chỉ rút lại được câu hỏi đang chờ Phòng QL HSSV trả lời.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }
        return ApiResponseDto<ThreadEscalationDto>.Ok((await LoadEscalationAsync(escalationId))!, "Đã rút lại câu hỏi.");
    }

    public async Task<ApiResponseDto<ThreadEscalationDto>> RelayEscalationAsync(int userId, int escalationId)
    {
        var (error, member) = await ResolveMonitorAsync<ThreadEscalationDto>(userId);
        if (error != null) return error;

        var escalation = await _context.ThreadEscalations
            .Include(item => item.Thread)
            .FirstOrDefaultAsync(item => item.Id == escalationId && item.Thread!.ClassId == member!.ClassId);
        if (escalation == null)
        {
            return Fail<ThreadEscalationDto>("Không tìm thấy câu hỏi đã chuyển của lớp bạn.",
                StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }
        if (escalation.Status != EscalationStatus.Answered || string.IsNullOrWhiteSpace(escalation.Answer))
        {
            return Fail<ThreadEscalationDto>("Chỉ chuyển lại được phản hồi chính thức đã nhận từ Phòng QL HSSV.",
                StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }
        if (escalation.RelayedAt.HasValue)
        {
            return Fail<ThreadEscalationDto>("Phản hồi chính thức này đã được chuyển vào lớp.",
                StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var claimed = await _context.ThreadEscalations
            .Where(item => item.Id == escalationId && item.Status == EscalationStatus.Answered && item.RelayedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RelayedAt, now)
                .SetProperty(item => item.RelayedById, userId));
        if (claimed == 0)
        {
            await transaction.RollbackAsync();
            return Fail<ThreadEscalationDto>("Phản hồi chính thức này đã được chuyển vào lớp.",
                StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        var threadReplies = await _context.ClassThreadReplies.Where(reply => reply.ThreadId == escalation.ThreadId).ToListAsync();
        foreach (var reply in threadReplies) reply.IsAnswer = false;
        _context.ClassThreadReplies.Add(new ClassThreadReply
        {
            ThreadId = escalation.ThreadId,
            AuthorId = userId,
            Body = $"Phản hồi chính thức từ Phòng QL HSSV:\n\n{escalation.Answer.Trim()}",
            IsAnswer = true,
            CreatedAt = now
        });
        escalation.RelayedAt = now;
        escalation.RelayedById = userId;
        escalation.Thread!.Status = ClassThreadStatus.Answered;
        escalation.Thread.LastActivityAt = now;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var result = await LoadEscalationAsync(escalationId);
        return ApiResponseDto<ThreadEscalationDto>.Ok(result!, "Đã chuyển phản hồi chính thức vào chủ đề lớp.");
    }

    private sealed record ClassMember(int ClassId, string ClassCode, string ClassName, string? MonitorName, int? MonitorId);

    /// <summary>Escalations are the class monitor's tool: the class's MonitorStudentId decides, not the role alone.</summary>
    private async Task<(ApiResponseDto<T>? Error, ClassMember? Member)> ResolveMonitorAsync<T>(int userId)
    {
        var (error, member) = await ResolveMemberAsync<T>(userId);
        if (error != null)
        {
            return (error, null);
        }
        return member!.MonitorId == userId
            ? (null, member)
            : (Fail<T>("Chỉ lớp trưởng mới chuyển câu hỏi lên Phòng QL HSSV.", StatusCodes.Status403Forbidden, ApiErrorCodes.AccessDenied), null);
    }

    private IQueryable<ThreadEscalation> EscalationQuery() => _context.ThreadEscalations.AsNoTracking()
        .Include(escalation => escalation.Thread)
        .Include(escalation => escalation.RaisedBy)
        .Include(escalation => escalation.AnsweredBy)
        .Include(escalation => escalation.RelayedBy);

    private async Task<ThreadEscalationDto?> LoadEscalationAsync(int escalationId)
    {
        var escalation = await EscalationQuery().FirstOrDefaultAsync(item => item.Id == escalationId);
        return escalation == null ? null : MapEscalation(escalation);
    }

    private static ThreadEscalationDto MapEscalation(ThreadEscalation escalation) => new()
    {
        Id = escalation.Id,
        ThreadId = escalation.ThreadId,
        ThreadTitle = escalation.Thread?.Title ?? string.Empty,
        Question = escalation.Question,
        Status = escalation.Status,
        RaisedByName = escalation.RaisedBy?.FullName,
        CreatedAt = escalation.CreatedAt,
        Answer = escalation.Answer,
        AnsweredByName = escalation.AnsweredBy?.FullName,
        AnsweredAt = escalation.AnsweredAt,
        RelayedByName = escalation.RelayedBy?.FullName,
        RelayedAt = escalation.RelayedAt,
        CanRelay = escalation.Status == EscalationStatus.Answered && !escalation.RelayedAt.HasValue,
        WithdrawnAt = escalation.WithdrawnAt
    };

    /// <summary>The signed-in student's class; students who are not in a class have no space yet.</summary>
    private async Task<(ApiResponseDto<T>? Error, ClassMember? Member)> ResolveMemberAsync<T>(int userId)
    {
        var member = await _context.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.ClassId != null)
            .Select(user => new ClassMember(
                user.ClassId!.Value,
                user.Class!.ClassCode,
                user.Class.ClassName,
                user.Class.MonitorStudent != null ? user.Class.MonitorStudent.FullName : null,
                user.Class.MonitorStudentId))
            .FirstOrDefaultAsync();
        return member == null
            ? (Fail<T>("Bạn chưa được xếp lớp nên chưa có không gian trao đổi.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation), null)
            : (null, member);
    }

    private async Task<ClassThreadDetailDto?> LoadDetailAsync(ClassMember member, int threadId, int userId)
    {
        var thread = await _context.ClassThreads.AsNoTracking()
            .Include(item => item.Author)
            .Include(item => item.Replies).ThenInclude(reply => reply.Author)
            .Include(item => item.Escalations)
            .FirstOrDefaultAsync(item => item.Id == threadId && item.ClassId == member.ClassId);
        if (thread == null)
        {
            return null;
        }

        var escalationStatus = thread.Escalations.OrderByDescending(escalation => escalation.Id).Select(escalation => escalation.Status).FirstOrDefault();
        var detail = FillSummary(new ClassThreadDetailDto(), thread, thread.Replies.Count, escalationStatus, userId);
        detail.Body = thread.Body;
        detail.CanManageAnswers = member.MonitorId == userId && thread.Kind == ClassThreadKind.Question;
        detail.Replies = thread.Replies.OrderBy(reply => reply.CreatedAt).ThenBy(reply => reply.Id)
            .Select(reply => MapReply(reply, reply.Author!, userId,
                detail.CanManageAnswers && thread.Status != ClassThreadStatus.Closed)).ToList();
        detail.CanDelete = thread.AuthorId == userId && thread.Replies.All(reply => reply.AuthorId == userId) && thread.Escalations.Count == 0;
        detail.CanEscalate = member.MonitorId == userId
            && ClassThreadKind.MemberKinds.Contains(thread.Kind)
            && escalationStatus != EscalationStatus.Pending;
        detail.CanPin = member.MonitorId == userId;
        return detail;
    }

    private static T FillSummary<T>(T target, ClassThread thread, int replyCount, string? escalationStatus, int userId) where T : ClassThreadSummaryDto
    {
        target.EscalationStatus = escalationStatus;
        target.Id = thread.Id;
        target.Kind = thread.Kind;
        target.Title = thread.Title;
        target.Preview = thread.Body.Length <= PreviewLength ? thread.Body : thread.Body[..PreviewLength] + "…";
        target.Status = thread.Status;
        target.IsPinned = thread.IsPinned;
        target.Author = MapAuthor(thread.Author!, userId);
        target.ReplyCount = replyCount;
        target.CreatedAt = thread.CreatedAt;
        target.LastActivityAt = thread.LastActivityAt;
        return target;
    }

    private static ClassThreadReplyDto MapReply(ClassThreadReply reply, User author, int userId, bool canMarkAnswer = false) => new()
    {
        Id = reply.Id,
        Body = reply.Body,
        Author = MapAuthor(author, userId),
        IsAnswer = reply.IsAnswer,
        CreatedAt = reply.CreatedAt,
        CanDelete = reply.AuthorId == userId && !reply.IsAnswer,
        CanMarkAnswer = canMarkAnswer && !reply.IsAnswer
    };

    private static PostAuthorDto MapAuthor(User user, int userId) => new()
    {
        Id = user.Id,
        StudentCode = user.StudentCode,
        FullName = user.FullName,
        IsMonitor = user.Role == Role.Monitor,
        IsMe = user.Id == userId
    };

    private static ApiResponseDto<T> ThreadNotFound<T>() =>
        Fail<T>("Không tìm thấy chủ đề trong lớp của bạn.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);

    private static ApiResponseDto<T> Fail<T>(string message, int status, string errorCode) =>
        ApiResponseDto<T>.Fail(message, status, errorCode);
}
