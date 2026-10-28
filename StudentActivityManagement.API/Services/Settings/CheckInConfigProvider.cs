using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;

namespace StudentActivityManagement.API.Services.Settings;

public sealed class CheckInConfigProvider : ICheckInConfigProvider
{
    private readonly AppDbContext _context;

    public CheckInConfigProvider(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EffectiveCheckInConfig> GetAsync(int activityId, CancellationToken cancellationToken = default)
    {
        var config = await _context.ActivityCheckInConfigs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ActivityId == activityId, cancellationToken);

        return config == null
            ? new EffectiveCheckInConfig(
                activityId,
                SettingDefaults.CheckInMethod,
                SettingDefaults.OpenBeforeMinutes,
                SettingDefaults.CloseAfterMinutes,
                IsDefault: true,
                UpdatedAt: null)
            : new EffectiveCheckInConfig(
                activityId,
                config.Method,
                config.OpenBeforeMinutes,
                config.CloseAfterMinutes,
                IsDefault: false,
                config.UpdatedAt);
    }
}
