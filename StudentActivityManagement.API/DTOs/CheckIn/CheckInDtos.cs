using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.CheckIn;

public sealed class StudentCheckInRequestDto
{
    [Required(ErrorMessage = "Nhập mã check-in.")]
    [StringLength(20, ErrorMessage = "Mã check-in tối đa 20 ký tự.")]
    public string Code { get; set; } = string.Empty;
}

/// <summary>Result of a successful check-in.</summary>
public sealed class CheckInResultDto
{
    public int ActivityId { get; set; }
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime CheckedInAt { get; set; }
    public string Method { get; set; } = string.Empty;
    /// <summary>Registration status after check-in (Attended).</summary>
    public string RegistrationStatus { get; set; } = string.Empty;
}

/// <summary>What the signed-in student sees about check-in for one activity.</summary>
public sealed class StudentCheckInStatusDto
{
    public int ActivityId { get; set; }
    public string ActivityStatus { get; set; } = string.Empty;
    /// <summary>Code (student enters the session code) or Manual (staff record attendance).</summary>
    public string Method { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public List<SessionCheckInStatusDto> Sessions { get; set; } = [];
}

public sealed class SessionCheckInStatusDto
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    /// <summary>True when the student can check in to this session right now.</summary>
    public bool CanCheckIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
}

/// <summary>Check-in codes of an activity for Phòng QL HSSV.</summary>
public sealed class ActivityCheckInCodesDto
{
    public int ActivityId { get; set; }
    public string ActivityStatus { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int OpenBeforeMinutes { get; set; }
    public int CloseAfterMinutes { get; set; }
    public List<SessionCheckInCodeDto> Sessions { get; set; } = [];
}

public sealed class SessionCheckInCodeDto
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public string? Code { get; set; }
    public DateTime? CodeGeneratedAt { get; set; }
    public int CheckInCount { get; set; }
}
