namespace StudentActivityManagement.API.DTOs.Student;

public sealed class StudentEvaluationPeriodDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TargetPoints { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ApprovedPoints { get; set; }
}

public sealed class StudentActivityHistoryDto
{
    public int RegistrationId { get; set; }
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public int TrainingPoints { get; set; }
    public string? PointStatus { get; set; }
    public int? PointRecordId { get; set; }
    public string? AppealStatus { get; set; }

    /// <summary>True when this rejected point decision can be appealed now (US-37 rules evaluated by the API).</summary>
    public bool CanAppeal { get; set; }
    public int? PointsAwarded { get; set; }
    public string? PointReason { get; set; }
    public DateTime? PointDecidedAt { get; set; }
    public int CheckInCount { get; set; }
    public List<StudentSessionHistoryDto> Sessions { get; set; } = [];
}

public sealed class StudentSessionHistoryDto
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string? CheckInMethod { get; set; }
}

public sealed class StudentEvaluationHistoryDto
{
    public StudentEvaluationPeriodDto Period { get; set; } = new();
    public List<StudentActivityHistoryDto> Activities { get; set; } = [];
}
