using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;

namespace StudentActivityManagement.Web.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Nhập mã sinh viên, mã cán bộ hoặc email.")]
    [Display(Name = "Mã đăng nhập hoặc email")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class ActivityListViewModel
{
    public PagedResult<ActivityDto> Page { get; set; } = new();
    public string? Search { get; set; }
    public string? Campus { get; set; }
    public string? Status { get; set; }
}

public sealed class ParticipantListViewModel
{
    public ActivityParticipants Data { get; set; } = new();
    public string? Status { get; set; }
    public string? Search { get; set; }
    public bool PendingOnly { get; set; }

    /// <summary>Attendance can be changed while the activity is open, running or just finished (same rule as the API).</summary>
    public bool CanEditAttendance => Data.ActivityStatus is "Published" or "Ongoing" or "Completed";
}

public sealed class ParticipantDetailViewModel
{
    public ActivityDto Activity { get; set; } = new();
    public ParticipantDetail Participant { get; set; } = new();

    public bool CanEditAttendance =>
        Activity.Status is "Published" or "Ongoing" or "Completed" && Participant.RegistrationStatus != "Cancelled";
}

public sealed class ActivityFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Nhập tên hoạt động."), StringLength(150)]
    [Display(Name = "Tên hoạt động")]
    public string ActivityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập mô tả.")]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Yêu cầu chuẩn bị tối đa 2000 ký tự.")]
    [Display(Name = "Yêu cầu cần chuẩn bị")]
    public string? Requirements { get; set; }

    [Required(ErrorMessage = "Nhập đơn vị tổ chức."), StringLength(120)]
    [Display(Name = "Đơn vị tổ chức")]
    public string OrganizerName { get; set; } = string.Empty;

    [Display(Name = "Mở đăng ký")]
    public DateTime RegistrationOpenTime { get; set; }

    [Display(Name = "Đóng đăng ký")]
    public DateTime RegistrationCloseTime { get; set; }

    [Range(1, 10000, ErrorMessage = "Sức chứa từ 1 đến 10.000.")]
    [Display(Name = "Sức chứa")]
    public int MaxParticipants { get; set; } = 50;

    [Range(0, 100, ErrorMessage = "Điểm rèn luyện từ 0 đến 100.")]
    [Display(Name = "Điểm rèn luyện")]
    public int TrainingPoints { get; set; } = 5;

    [Required(ErrorMessage = "Mô tả ngắn đối tượng tham gia."), StringLength(200)]
    [Display(Name = "Đối tượng (mô tả)")]
    public string TargetAudience { get; set; } = "Tất cả sinh viên";

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Draft";

    public List<SessionFormItem> Sessions { get; set; } = [];
    public List<AudienceRuleFormItem> AudienceRules { get; set; } = [];

    // Display only
    public int CurrentParticipantsCount { get; set; }
    /// <summary>Statuses the status dropdown offers: Draft/Published on create, current + API-allowed next statuses on edit.</summary>
    public List<string> StatusOptions { get; set; } = ["Draft", "Published"];
    public List<ClassDto> Classes { get; set; } = [];

    public static ActivityFormViewModel NewDefault()
    {
        var today = VnTime.Now.Date;
        return new ActivityFormViewModel
        {
            RegistrationOpenTime = VnTime.Now.AddMinutes(-5).AddSeconds(-VnTime.Now.Second),
            RegistrationCloseTime = today.AddDays(6).AddHours(23).AddMinutes(59),
            Sessions = [new SessionFormItem { SessionName = "Buổi 1", Campus = "Cơ sở 1", StartTime = today.AddDays(7).AddHours(8), EndTime = today.AddDays(7).AddHours(11), SortOrder = 1 }],
        };
    }

    public static ActivityFormViewModel From(ActivityDto activity) => new()
    {
        Id = activity.Id,
        ActivityName = activity.ActivityName,
        Description = activity.Description,
        Requirements = activity.Requirements,
        OrganizerName = activity.OrganizerName,
        RegistrationOpenTime = VnTime.FromUtc(activity.RegistrationOpenTime),
        RegistrationCloseTime = VnTime.FromUtc(activity.RegistrationCloseTime),
        MaxParticipants = activity.MaxParticipants,
        TrainingPoints = activity.TrainingPoints,
        TargetAudience = activity.TargetAudience,
        Status = activity.Status,
        StatusOptions = [activity.Status, .. activity.AllowedNextStatuses ?? []],
        CurrentParticipantsCount = activity.CurrentParticipantsCount,
        Sessions = activity.Sessions.OrderBy(s => s.SortOrder).Select(s => new SessionFormItem
        {
            Id = s.Id,
            SessionName = s.SessionName,
            Campus = s.Campus,
            Location = s.Location,
            StartTime = VnTime.FromUtc(s.StartTime),
            EndTime = VnTime.FromUtc(s.EndTime),
            SortOrder = s.SortOrder
        }).ToList(),
        AudienceRules = activity.AudienceRules.Select(r => new AudienceRuleFormItem
        {
            Campus = r.Campus,
            AcademicYear = r.AcademicYear,
            ClassId = r.ClassId,
            ParticipationType = r.ParticipationType
        }).ToList(),
    };

    /// <summary>Form values are Vietnam wall-clock time; the API receives them with the +07:00 offset.</summary>
    public ActivityWriteRequest ToRequest() => new()
    {
        ActivityName = ActivityName.Trim(),
        Description = Description.Trim(),
        Requirements = Requirements?.Trim() ?? string.Empty,
        OrganizerName = OrganizerName.Trim(),
        RegistrationOpenTime = VnTime.ToOffset(RegistrationOpenTime),
        RegistrationCloseTime = VnTime.ToOffset(RegistrationCloseTime),
        MaxParticipants = MaxParticipants,
        TrainingPoints = TrainingPoints,
        TargetAudience = TargetAudience.Trim(),
        Status = Status,
        Sessions = Sessions.Select((s, index) => new SessionWriteRequest
        {
            Id = s.Id,
            SessionName = s.SessionName.Trim(),
            Campus = s.Campus.Trim(),
            Location = s.Location.Trim(),
            StartTime = VnTime.ToOffset(s.StartTime),
            EndTime = VnTime.ToOffset(s.EndTime),
            SortOrder = index + 1
        }).ToList(),
        AudienceRules = AudienceRules
            .Where(r => !string.IsNullOrWhiteSpace(r.Campus) || !string.IsNullOrWhiteSpace(r.AcademicYear) || r.ClassId.HasValue)
            .Select(r => new AudienceRuleWriteRequest
            {
                Campus = string.IsNullOrWhiteSpace(r.Campus) ? null : r.Campus.Trim(),
                AcademicYear = string.IsNullOrWhiteSpace(r.AcademicYear) ? null : r.AcademicYear.Trim(),
                ClassId = r.ClassId,
                ParticipationType = r.ParticipationType
            }).ToList(),
    };
}

public sealed class SessionFormItem
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Nhập tên buổi.")]
    public string SessionName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập cơ sở.")]
    public string Campus { get; set; } = "Cơ sở 1";

    [Required(ErrorMessage = "Nhập địa điểm.")]
    public string Location { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AudienceRuleFormItem
{
    public string? Campus { get; set; }
    public string? AcademicYear { get; set; }
    public int? ClassId { get; set; }
    public string ParticipationType { get; set; } = "Required";
}

// ---- Student Evidences (US-08)
public sealed class StudentEvidenceViewModel
{
    public ActivityDto Activity { get; set; } = new();
    public List<StudentEvidence> Evidences { get; set; } = [];
    public bool CanUpload { get; set; }
}
