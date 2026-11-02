using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Services;

public sealed class ActivitySettingsService : IActivitySettingsService
{
    private static readonly string[] EditableStatuses = [ActivityStatus.Draft, ActivityStatus.Published, ActivityStatus.Ongoing];

    private readonly AppDbContext _context;
    private readonly ICheckInConfigProvider _configProvider;

    public ActivitySettingsService(AppDbContext context, ICheckInConfigProvider configProvider)
    {
        _context = context;
        _configProvider = configProvider;
    }

    public async Task<ApiResponseDto<CheckInConfigDto>> GetCheckInConfigAsync(int activityId)
    {
        if (!await _context.Activities.AsNoTracking().AnyAsync(item => item.Id == activityId))
            return NotFound<CheckInConfigDto>();

        var effective = await _configProvider.GetAsync(activityId);
        var hasCheckIns = await _context.ActivityCheckIns.AsNoTracking()
            .AnyAsync(item => item.Session != null && item.Session.ActivityId == activityId);
        return ApiResponseDto<CheckInConfigDto>.Ok(new CheckInConfigDto
        {
            ActivityId = activityId,
            Method = effective.Method,
            OpenBeforeMinutes = effective.OpenBeforeMinutes,
            CloseAfterMinutes = effective.CloseAfterMinutes,
            IsDefault = effective.IsDefault,
            HasCheckIns = hasCheckIns,
            UpdatedAt = effective.UpdatedAt
        });
    }

    public async Task<ApiResponseDto<CheckInConfigDto>> UpdateCheckInConfigAsync(
        int activityId, CheckInConfigUpdateDto request, int updatedById)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(item => item.Id == activityId);
        if (activity == null) return NotFound<CheckInConfigDto>();
        if (!EditableStatuses.Contains(activity.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Conflict<CheckInConfigDto>("Không thể sửa cấu hình check-in của hoạt động đã kết thúc hoặc đã hủy.");
        }

        var method = CheckInMethods.Normalize(request.Method);
        if (method == null)
        {
            return Validation<CheckInConfigDto>("Phương thức check-in chỉ nhận Code hoặc Manual.");
        }

        var hasCheckIns = await _context.ActivityCheckIns.AnyAsync(item => item.Session!.ActivityId == activityId);
        var config = await _context.ActivityCheckInConfigs.FirstOrDefaultAsync(item => item.ActivityId == activityId);
        var currentMethod = config?.Method ?? SettingDefaults.CheckInMethod;
        if (hasCheckIns && !string.Equals(method, currentMethod, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict<CheckInConfigDto>("Không thể đổi phương thức sau khi hoạt động đã có check-in.");
        }

        var now = DateTime.UtcNow;
        if (config == null)
        {
            config = new ActivityCheckInConfig { ActivityId = activityId };
            _context.ActivityCheckInConfigs.Add(config);
        }

        config.Method = method;
        config.OpenBeforeMinutes = request.OpenBeforeMinutes;
        config.CloseAfterMinutes = request.CloseAfterMinutes;
        config.UpdatedAt = now;
        config.UpdatedById = updatedById;
        await _context.SaveChangesAsync();

        return ApiResponseDto<CheckInConfigDto>.Ok(new CheckInConfigDto
        {
            ActivityId = activityId,
            Method = config.Method,
            OpenBeforeMinutes = config.OpenBeforeMinutes,
            CloseAfterMinutes = config.CloseAfterMinutes,
            IsDefault = false,
            HasCheckIns = hasCheckIns,
            UpdatedAt = config.UpdatedAt
        }, "Đã lưu cấu hình check-in.");
    }

    public async Task<ApiResponseDto<ReminderScheduleDto>> GetReminderScheduleAsync(int activityId)
    {
        if (!await _context.Activities.AsNoTracking().AnyAsync(item => item.Id == activityId))
            return NotFound<ReminderScheduleDto>();

        var offsets = await _context.ActivityReminderOffsets.AsNoTracking()
            .Where(item => item.ActivityId == activityId)
            .OrderBy(item => item.OffsetMinutes)
            .Select(item => item.OffsetMinutes)
            .ToListAsync();
        var isDefault = offsets.Count == 0;
        return ApiResponseDto<ReminderScheduleDto>.Ok(new ReminderScheduleDto
        {
            ActivityId = activityId,
            OffsetsMinutes = isDefault ? SettingDefaults.ReminderOffsetsMinutes.ToList() : offsets,
            IsDefault = isDefault
        });
    }

    public async Task<ApiResponseDto<ReminderScheduleDto>> UpdateReminderScheduleAsync(
        int activityId, ReminderScheduleUpdateDto request)
    {
        if (!await _context.Activities.AnyAsync(item => item.Id == activityId))
            return NotFound<ReminderScheduleDto>();

        var offsets = request.OffsetsMinutes;
        if (offsets.Count == 0 || offsets.Count > SettingDefaults.MaxReminderCount ||
            offsets.Distinct().Count() != offsets.Count ||
            offsets.Any(value => value < SettingDefaults.MinReminderOffsetMinutes || value > SettingDefaults.MaxReminderOffsetMinutes))
        {
            return Validation<ReminderScheduleDto>(
                $"Chọn từ 1 đến {SettingDefaults.MaxReminderCount} mốc nhắc khác nhau, trong khoảng {SettingDefaults.MinReminderOffsetMinutes}–{SettingDefaults.MaxReminderOffsetMinutes} phút.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var existing = await _context.ActivityReminderOffsets.Where(item => item.ActivityId == activityId).ToListAsync();
        _context.ActivityReminderOffsets.RemoveRange(existing);
        var sorted = offsets.Order().ToList();
        _context.ActivityReminderOffsets.AddRange(sorted.Select(offset => new ActivityReminderOffset
        {
            ActivityId = activityId,
            OffsetMinutes = offset
        }));
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ApiResponseDto<ReminderScheduleDto>.Ok(new ReminderScheduleDto
        {
            ActivityId = activityId,
            OffsetsMinutes = sorted,
            IsDefault = false
        }, "Đã lưu lịch nhắc.");
    }

    private static ApiResponseDto<T> NotFound<T>() => ApiResponseDto<T>.Fail(
        "Không tìm thấy hoạt động.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);

    private static ApiResponseDto<T> Conflict<T>(string message) => ApiResponseDto<T>.Fail(
        message, StatusCodes.Status409Conflict, ApiErrorCodes.BusinessRuleViolation);

    private static ApiResponseDto<T> Validation<T>(string message) => ApiResponseDto<T>.Fail(
        message, StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);
}
