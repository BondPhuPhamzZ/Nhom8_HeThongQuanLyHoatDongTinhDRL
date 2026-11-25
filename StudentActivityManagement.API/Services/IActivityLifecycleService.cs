using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Scoring;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Services;

public interface IActivityLifecycleService
{
    Task<int> RunOnceAsync(CancellationToken ct = default);
    Task<ApiResponseDto<FinalizeResultDto>> FinalizeAsync(int activityId, int? staffId, CancellationToken ct = default);
    ActivityPhaseInfo GetPhase(Activity activity, IEnumerable<ActivitySession> sessions, EffectiveCheckInConfig config);
}

public sealed record ActivityPhaseInfo(string Phase, DateTime CheckInClosesAt, DateTime PointsFinalizeDueAt)
{
    public string PhaseLabel => ActivityPhase.Label(Phase);
}
