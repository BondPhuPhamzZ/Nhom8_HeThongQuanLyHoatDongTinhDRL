using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Scoring;

public sealed class EvaluationPeriodDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TargetPoints { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Number of score decisions already recorded in the period.</summary>
    public int PointRecordCount { get; set; }
}

public sealed class EvaluationPeriodWriteDto
{
    [Required(ErrorMessage = "Nhập tên kỳ đánh giá.")]
    [StringLength(100, ErrorMessage = "Tên kỳ tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Range(0, 1000, ErrorMessage = "Điểm mục tiêu từ 0 đến 1000.")]
    public int TargetPoints { get; set; }
}

public sealed class EvaluationPeriodStatusDto
{
    [Required(ErrorMessage = "Chọn trạng thái Open hoặc Closed.")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>Scoring sheet of one activity: who can receive points and what has been decided.</summary>
public sealed class ActivityScoringDto
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string ActivityStatus { get; set; } = string.Empty;
    public int TrainingPoints { get; set; }
    public EvaluationPeriodDto? Period { get; set; }
    /// <summary>True when approvals are possible now (activity Completed, period found and Open).</summary>
    public bool CanApprove { get; set; }
    /// <summary>Why approvals are not possible; null when <see cref="CanApprove"/> is true.</summary>
    public string? BlockedReason { get; set; }
    public int EligibleCount { get; set; }
    public int ApprovedCount { get; set; }
    public List<ScoringCandidateDto> Candidates { get; set; } = [];
}

public sealed class ScoringCandidateDto
{
    public int RegistrationId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
    public int CheckInCount { get; set; }
    public int AcceptedEvidenceCount { get; set; }
    public int PendingEvidenceCount { get; set; }
    /// <summary>Attended, or at least one accepted evidence, and no decision yet.</summary>
    public bool Eligible { get; set; }
    public string? IneligibleReason { get; set; }
    public PointRecordDto? PointRecord { get; set; }
}

public sealed class PointRecordDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public int StudentId { get; set; }
    public int ActivityId { get; set; }
    public int PeriodId { get; set; }
    public int Points { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime DecidedAt { get; set; }
}

public sealed class PointRecordHistoryDto
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public int Points { get; set; }
    public string? Reason { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class ApprovePointsRequestDto
{
    /// <summary>Registrations to approve; empty or null approves every eligible registration of the activity.</summary>
    public List<int>? RegistrationIds { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Note { get; set; }
}

public sealed class ApprovePointsResultDto
{
    public int ApprovedCount { get; set; }
    public List<SkippedRegistrationDto> Skipped { get; set; } = [];
}

public sealed class SkippedRegistrationDto
{
    public int RegistrationId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
