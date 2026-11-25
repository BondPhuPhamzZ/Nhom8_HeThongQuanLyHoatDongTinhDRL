using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Models;

public static class ActivityPhase
{
    public const string Draft = "Draft";
    public const string Cancelled = "Cancelled";
    public const string Finalized = "Finalized";
    public const string AttendanceReview = "AttendanceReview";
    public const string Ongoing = "Ongoing";
    public const string RegistrationClosed = "RegistrationClosed";
    public const string RegistrationOpen = "RegistrationOpen";
    public const string Upcoming = "Upcoming";

    public static string Resolve(Activity activity, IEnumerable<ActivitySession> sessions,
        EffectiveCheckInConfig config, DateTime utcNow)
    {
        if (activity.Status == ActivityStatus.Draft) return Draft;
        if (activity.Status == ActivityStatus.Cancelled) return Cancelled;
        if (activity.PointsFinalizedAt.HasValue) return Finalized;
        if (utcNow >= CheckInClosesAt(activity, sessions, config)) return AttendanceReview;
        if (utcNow >= activity.StartTime) return Ongoing;
        if (utcNow > activity.RegistrationCloseTime) return RegistrationClosed;
        return utcNow >= activity.RegistrationOpenTime ? RegistrationOpen : Upcoming;
    }

    public static DateTime CheckInClosesAt(Activity activity, IEnumerable<ActivitySession> sessions,
        EffectiveCheckInConfig config) =>
        sessions.Select(config.ClosesAt).DefaultIfEmpty(activity.EndTime.AddMinutes(config.CloseAfterMinutes)).Max();

    public static DateTime FinalizeDueAt(Activity activity, IEnumerable<ActivitySession> sessions,
        EffectiveCheckInConfig config) => CheckInClosesAt(activity, sessions, config).AddMinutes(config.CorrectionWindowMinutes);

    public static string Label(string phase) => phase switch
    {
        Draft => "Nháp",
        Cancelled => "Đã hủy",
        Finalized => "Đã chốt điểm",
        AttendanceReview => "Đã kết thúc, đang đối chiếu điểm danh",
        Ongoing => "Đang diễn ra",
        RegistrationClosed => "Đã đóng đăng ký, chờ diễn ra",
        RegistrationOpen => "Đang mở đăng ký",
        Upcoming => "Sắp mở đăng ký",
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    public static bool AllowsStudentCheckIn(string phase) =>
        phase is RegistrationOpen or RegistrationClosed or Ongoing;
}
