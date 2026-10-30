namespace StudentActivityManagement.API.DTOs.Reports;

/// <summary>Filters shared by every report (query string). Without <see cref="PeriodId"/> the current period is used.</summary>
public sealed class ReportQueryDto
{
    public int? PeriodId { get; set; }
    public string? Campus { get; set; }
    public string? AcademicYear { get; set; }
    public int? ClassId { get; set; }
    /// <summary>Overview breakdown: class (default), academicYear or campus.</summary>
    public string? GroupBy { get; set; }
}

public sealed class ReportPeriodDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TargetPoints { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Headline numbers of a period for the filtered students, plus a breakdown by class, cohort or campus.</summary>
public sealed class ReportOverviewDto
{
    public ReportPeriodDto Period { get; set; } = new();
    /// <summary>Activities ending in the period, Draft and Cancelled excluded.</summary>
    public int ActivityCount { get; set; }
    public int CancelledActivityCount { get; set; }
    public int StudentCount { get; set; }
    public int RegistrationCount { get; set; }
    public int AttendedCount { get; set; }
    /// <summary>Attended / registrations in percent (one decimal); null without registrations.</summary>
    public double? ParticipationRate { get; set; }
    public int ApprovedPoints { get; set; }
    public double AveragePoints { get; set; }
    public int StudentsMeetingTarget { get; set; }
    public int AtRiskCount { get; set; }
    public string GroupBy { get; set; } = string.Empty;
    public List<ReportGroupRowDto> Groups { get; set; } = [];
}

public sealed class ReportGroupRowDto
{
    /// <summary>Class id, cohort or campus; empty for students without that value.</summary>
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int RegistrationCount { get; set; }
    public int AttendedCount { get; set; }
    public double? ParticipationRate { get; set; }
    public int ApprovedPoints { get; set; }
    public double AveragePoints { get; set; }
    public int AtRiskCount { get; set; }
}

public sealed class ActivityReportRowDto
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EndTime { get; set; }
    public int TrainingPoints { get; set; }
    public int RegistrationCount { get; set; }
    public int AttendedCount { get; set; }
    public double? ParticipationRate { get; set; }
    public int ApprovedCount { get; set; }
    public int ApprovedPoints { get; set; }
}

public sealed class AtRiskStudentDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public int Points { get; set; }
    public int TargetPoints { get; set; }
    public int MissingPoints { get; set; }
    public int RegistrationCount { get; set; }
    public int AttendedCount { get; set; }
}
