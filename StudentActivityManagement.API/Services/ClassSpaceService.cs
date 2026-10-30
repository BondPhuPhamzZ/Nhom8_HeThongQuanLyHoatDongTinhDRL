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
            .Select(thread => new { Thread = thread, ReplyCount = thread.Replies.Count })
            .ToListAsync();

        return ApiResponseDto<ClassSpaceDto>.Ok(new ClassSpaceDto
        {
            ClassId = member!.ClassId,
            ClassCode = member.ClassCode,
            ClassName = member.ClassName,
            MonitorName = member.MonitorName,
            Threads = new PagedResultDto<ClassThreadSummaryDto>
            {
                Items = threads.Select(item => FillSummary(new ClassThreadSummaryDto(), item.Thread, item.ReplyCount, userId)).ToList(),
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

        var detail = await LoadDetailAsync(member!.ClassId, threadId, userId);
        return detail == null ? ThreadNotFound<ClassThreadDetailDto>() : ApiResponseDto<ClassThreadDetailDto>.Ok(detail);
    }

    public async Task<ApiResponseDto<ClassThreadDetailDto>> CreateThreadAsync(int userId, ClassThreadCreateDto request)
    {
        var (error, member) = await ResolveMemberAsync<ClassThreadDetailDto>(userId);
        if (error != null)
        {
            return error;
        }

        var kind = ClassThreadKind.MemberKinds.FirstOrDefault(item => item.Equals(request.Kind?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (kind == null)
        {
            return Fail<ClassThreadDetailDto>("Loại bài chỉ nhận Question (câu hỏi) hoặc Discussion (thảo luận).",
                StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
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
            CreatedAt = now,
            LastActivityAt = now
        };
        _context.ClassThreads.Add(thread);
        await _context.SaveChangesAsync();

        var detail = await LoadDetailAsync(member.ClassId, thread.Id, userId);
        return ApiResponseDto<ClassThreadDetailDto>.Ok(detail!, kind == ClassThreadKind.Question ? "Đã gửi câu hỏi cho lớp." : "Đã mở chủ đề thảo luận.");
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

    private sealed record ClassMember(int ClassId, string ClassCode, string ClassName, string? MonitorName);

    /// <summary>The signed-in student's class; students who are not in a class have no space yet.</summary>
    private async Task<(ApiResponseDto<T>? Error, ClassMember? Member)> ResolveMemberAsync<T>(int userId)
    {
        var member = await _context.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.ClassId != null)
            .Select(user => new ClassMember(
                user.ClassId!.Value,
                user.Class!.ClassCode,
                user.Class.ClassName,
                user.Class.MonitorStudent != null ? user.Class.MonitorStudent.FullName : null))
            .FirstOrDefaultAsync();
        return member == null
            ? (Fail<T>("Bạn chưa được xếp lớp nên chưa có không gian trao đổi.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation), null)
            : (null, member);
    }

    private async Task<ClassThreadDetailDto?> LoadDetailAsync(int classId, int threadId, int userId)
    {
        var thread = await _context.ClassThreads.AsNoTracking()
            .Include(item => item.Author)
            .Include(item => item.Replies).ThenInclude(reply => reply.Author)
            .FirstOrDefaultAsync(item => item.Id == threadId && item.ClassId == classId);
        if (thread == null)
        {
            return null;
        }

        var detail = FillSummary(new ClassThreadDetailDto(), thread, thread.Replies.Count, userId);
        detail.Body = thread.Body;
        detail.Replies = thread.Replies.OrderBy(reply => reply.CreatedAt).ThenBy(reply => reply.Id)
            .Select(reply => MapReply(reply, reply.Author!, userId)).ToList();
        detail.CanDelete = thread.AuthorId == userId && thread.Replies.All(reply => reply.AuthorId == userId);
        return detail;
    }

    private static T FillSummary<T>(T target, ClassThread thread, int replyCount, int userId) where T : ClassThreadSummaryDto
    {
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

    private static ClassThreadReplyDto MapReply(ClassThreadReply reply, User author, int userId) => new()
    {
        Id = reply.Id,
        Body = reply.Body,
        Author = MapAuthor(author, userId),
        IsAnswer = reply.IsAnswer,
        CreatedAt = reply.CreatedAt,
        CanDelete = reply.AuthorId == userId && !reply.IsAnswer
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
