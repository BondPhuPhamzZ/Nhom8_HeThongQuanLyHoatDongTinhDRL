using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services.Settings;

/// <summary>
/// Single source of the check-in settings in effect for an activity: the stored row, or the defaults.
/// Check-in (US-07) and the settings API (US-26) both read through this.
/// </summary>
public interface ICheckInConfigProvider
{
    Task<EffectiveCheckInConfig> GetAsync(int activityId, CancellationToken cancellationToken = default);
}

public sealed record EffectiveCheckInConfig(
    int ActivityId,
    string Method,
    int OpenBeforeMinutes,
    int CloseAfterMinutes,
    bool IsDefault,
    DateTime? UpdatedAt)
{
    public DateTime OpensAt(ActivitySession session) => session.StartTime.AddMinutes(-OpenBeforeMinutes);

    public DateTime ClosesAt(ActivitySession session) => session.EndTime.AddMinutes(CloseAfterMinutes);

    public bool IsOpen(ActivitySession session, DateTime utcNow) => utcNow >= OpensAt(session) && utcNow <= ClosesAt(session);
}
