using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed class EvaluationPeriodService : IEvaluationPeriodService
{
    private readonly AppDbContext _context;

    public EvaluationPeriodService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponseDto<List<EvaluationPeriodDto>>> GetPeriodsAsync()
    {
        var periods = await _context.EvaluationPeriods.AsNoTracking()
            .OrderByDescending(period => period.StartDate)
            .Select(period => new EvaluationPeriodDto
            {
                Id = period.Id,
                Name = period.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                TargetPoints = period.TargetPoints,
                Status = period.Status,
                PointRecordCount = _context.PointRecords.Count(record => record.PeriodId == period.Id)
            })
            .ToListAsync();
        return ApiResponseDto<List<EvaluationPeriodDto>>.Ok(periods);
    }

    public async Task<ApiResponseDto<EvaluationPeriodDto>> CreatePeriodAsync(EvaluationPeriodWriteDto request)
    {
        var error = await ValidateAsync(request, excludeId: null);
        if (error != null)
        {
            return error;
        }

        var period = new EvaluationPeriod
        {
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TargetPoints = request.TargetPoints,
            Status = EvaluationPeriodStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        _context.EvaluationPeriods.Add(period);
        await _context.SaveChangesAsync();
        return ApiResponseDto<EvaluationPeriodDto>.Ok(Map(period, 0), "Tạo kỳ đánh giá thành công.");
    }

    public async Task<ApiResponseDto<EvaluationPeriodDto>> UpdatePeriodAsync(int id, EvaluationPeriodWriteDto request)
    {
        var period = await _context.EvaluationPeriods.FirstOrDefaultAsync(item => item.Id == id);
        if (period == null)
        {
            return NotFound();
        }

        var error = await ValidateAsync(request, excludeId: id);
        if (error != null)
        {
            return error;
        }

        // Moving the dates would silently move already-recorded activities to another period.
        var recordCount = await _context.PointRecords.CountAsync(record => record.PeriodId == id);
        if (recordCount > 0 && (period.StartDate != request.StartDate || period.EndDate != request.EndDate))
        {
            return Fail("Kỳ đã có điểm được ghi nên không thể đổi thời gian; chỉ sửa được tên và điểm mục tiêu.",
                StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        period.Name = request.Name.Trim();
        period.StartDate = request.StartDate;
        period.EndDate = request.EndDate;
        period.TargetPoints = request.TargetPoints;
        await _context.SaveChangesAsync();
        return ApiResponseDto<EvaluationPeriodDto>.Ok(Map(period, recordCount), "Cập nhật kỳ đánh giá thành công.");
    }

    public async Task<ApiResponseDto<EvaluationPeriodDto>> SetStatusAsync(int id, string status)
    {
        var normalized = EvaluationPeriodStatus.All.FirstOrDefault(item => item.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalized == null)
        {
            return Fail("Trạng thái kỳ chỉ nhận Open hoặc Closed.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }

        var period = await _context.EvaluationPeriods.FirstOrDefaultAsync(item => item.Id == id);
        if (period == null)
        {
            return NotFound();
        }

        period.Status = normalized;
        await _context.SaveChangesAsync();
        var recordCount = await _context.PointRecords.CountAsync(record => record.PeriodId == id);
        return ApiResponseDto<EvaluationPeriodDto>.Ok(Map(period, recordCount),
            normalized == EvaluationPeriodStatus.Closed ? "Đã khóa kỳ đánh giá." : "Đã mở lại kỳ đánh giá.");
    }

    private async Task<ApiResponseDto<EvaluationPeriodDto>?> ValidateAsync(EvaluationPeriodWriteDto request, int? excludeId)
    {
        if (request.EndDate <= request.StartDate)
        {
            return Fail("Ngày kết thúc kỳ phải sau ngày bắt đầu.", StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
        }

        var name = request.Name.Trim();
        if (await _context.EvaluationPeriods.AnyAsync(period => period.Id != excludeId && period.Name == name))
        {
            return Fail("Tên kỳ đánh giá đã tồn tại.", StatusCodes.Status409Conflict, ApiErrorCodes.ResourceConflict);
        }

        // Periods must not overlap, otherwise an activity could belong to two periods.
        if (await _context.EvaluationPeriods.AnyAsync(period =>
                period.Id != excludeId && period.StartDate < request.EndDate && request.StartDate < period.EndDate))
        {
            return Fail("Thời gian kỳ bị trùng với một kỳ đánh giá khác.", StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);
        }

        return null;
    }

    private static EvaluationPeriodDto Map(EvaluationPeriod period, int recordCount) => new()
    {
        Id = period.Id,
        Name = period.Name,
        StartDate = period.StartDate,
        EndDate = period.EndDate,
        TargetPoints = period.TargetPoints,
        Status = period.Status,
        PointRecordCount = recordCount
    };

    private static ApiResponseDto<EvaluationPeriodDto> NotFound() =>
        Fail("Không tìm thấy kỳ đánh giá.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);

    private static ApiResponseDto<EvaluationPeriodDto> Fail(string message, int status, string errorCode) =>
        ApiResponseDto<EvaluationPeriodDto>.Fail(message, status, errorCode);
}
