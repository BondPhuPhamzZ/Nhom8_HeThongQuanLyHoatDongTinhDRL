using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.DTOs.Participants;

/// <summary>Participants page of one activity: counters for the whole activity plus one page of registrations.</summary>
public sealed class ActivityParticipantsDto
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string ActivityStatus { get; set; } = string.Empty;
    public ParticipantSummaryDto Summary { get; set; } = new();
    public List<SessionAttendanceDto> Sessions { get; set; } = [];
    public PagedResultDto<ParticipantDto> Participants { get; set; } = new();
}

public sealed class ParticipantSummaryDto
{
    /// <summary>Registrations still holding a place (Registered + Attended).</summary>
    public int Active { get; set; }
    public int Registered { get; set; }
    public int Attended { get; set; }
    public int Cancelled { get; set; }
    public int PendingEvidence { get; set; }
}

public sealed class SessionAttendanceDto
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int CheckInCount { get; set; }
}

public class ParticipantDto
{
    public int RegistrationId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string Campus { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public List<ParticipantCheckInDto> CheckIns { get; set; } = [];
    public int PendingEvidenceCount { get; set; }
    public int AcceptedEvidenceCount { get; set; }
    public int RejectedEvidenceCount { get; set; }
}

public sealed class ParticipantDetailDto : ParticipantDto
{
    public List<StaffEvidenceDto> Evidences { get; set; } = [];
}

public sealed class ParticipantCheckInDto
{
    public int SessionId { get; set; }
    public DateTime CheckedInAt { get; set; }
    /// <summary>Code (student entered the code) or Manual (recorded by staff).</summary>
    public string Method { get; set; } = string.Empty;
    public string? RecordedByName { get; set; }
}

public sealed class StaffEvidenceDto
{
    public int Id { get; set; }
    public int? SessionId { get; set; }
    public string? SessionName { get; set; }
    public string EvidenceType { get; set; } = string.Empty;
    public string? ConfirmationCode { get; set; }
    public string? Note { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    /// <summary>Staff download URL, or null for a confirmation code.</summary>
    public string? FileUrl { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UploadedAt { get; set; }
}

public sealed class ManualCheckInRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Thiếu mã đăng ký.")]
    public int RegistrationId { get; set; }
}

public sealed class EvidenceReviewRequestDto
{
    /// <summary>Accepted, Rejected, or Pending to reopen the review.</summary>
    [Required(ErrorMessage = "Chọn kết quả duyệt.")]
    public string Status { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Note { get; set; }
}
