using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.ClassSpace;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed class StaffEscalationService : IStaffEscalationService
{
    private readonly AppDbContext _context;

    public StaffEscalationService(AppDbContext context) => _context = context;

    public async Task<ApiResponseDto<PagedResultDto<ThreadEscalationDto>>> GetInboxAsync(string? status, int pageIndex, int pageSize)
    {
        var query = Query();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = EscalationStatus.All.FirstOrDefault(value => value.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalized == null)
            {
                return ApiResponseDto<PagedResultDto<ThreadEscalationDto>>.Fail(
                    "Trạng thái chỉ nhận Pending, Answered hoặc Withdrawn.",
                    StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
            }
            query = query.Where(item => item.Status == normalized);
        }

        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
        return ApiResponseDto<PagedResultDto<ThreadEscalationDto>>.Ok(new PagedResultDto<ThreadEscalationDto>
        {
            Items = items.Select(Map).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponseDto<ThreadEscalationDto>> GetAsync(int escalationId)
    {
        var item = await Query().FirstOrDefaultAsync(value => value.Id == escalationId);
        return item == null ? NotFound() : ApiResponseDto<ThreadEscalationDto>.Ok(Map(item));
    }

    public async Task<ApiResponseDto<ThreadEscalationDto>> AnswerAsync(int staffUserId, int escalationId, StaffEscalationAnswerDto request)
    {
        var answer = request.Answer?.Trim() ?? string.Empty;
        if (answer.Length is < 5 or > 4000)
        {
            return ApiResponseDto<ThreadEscalationDto>.Fail("Phản hồi từ 5 đến 4000 ký tự.",
                StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }

        // A conditional update prevents answering an escalation that the monitor withdrew concurrently.
        var updated = await _context.ThreadEscalations
            .Where(item => item.Id == escalationId && item.Status == EscalationStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, EscalationStatus.Answered)
                .SetProperty(item => item.Answer, answer)
                .SetProperty(item => item.AnsweredById, staffUserId)
                .SetProperty(item => item.AnsweredAt, DateTime.UtcNow));

        if (updated == 0)
        {
            var exists = await _context.ThreadEscalations.AnyAsync(item => item.Id == escalationId);
            return exists
                ? ApiResponseDto<ThreadEscalationDto>.Fail("Chỉ trả lời được câu hỏi đang chờ xử lý.",
                    StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation)
                : NotFound();
        }

        var item = await Query().SingleAsync(value => value.Id == escalationId);
        return ApiResponseDto<ThreadEscalationDto>.Ok(Map(item), "Đã trả lời câu hỏi của lớp trưởng.");
    }

    private IQueryable<ThreadEscalation> Query() => _context.ThreadEscalations.AsNoTracking()
        .Include(item => item.Thread).Include(item => item.RaisedBy).Include(item => item.AnsweredBy);

    private static ThreadEscalationDto Map(ThreadEscalation item) => new()
    {
        Id = item.Id,
        ThreadId = item.ThreadId,
        ThreadTitle = item.Thread?.Title ?? string.Empty,
        Question = item.Question,
        Status = item.Status,
        RaisedByName = item.RaisedBy?.FullName,
        CreatedAt = item.CreatedAt,
        Answer = item.Answer,
        AnsweredByName = item.AnsweredBy?.FullName,
        AnsweredAt = item.AnsweredAt,
        WithdrawnAt = item.WithdrawnAt
    };

    private static ApiResponseDto<ThreadEscalationDto> NotFound() =>
        ApiResponseDto<ThreadEscalationDto>.Fail("Không tìm thấy câu hỏi đã chuyển.",
            StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
}
