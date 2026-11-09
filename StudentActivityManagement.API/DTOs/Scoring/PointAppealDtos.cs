using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Scoring;

public sealed class CreatePointAppealDto
{
    [Range(1, int.MaxValue)] public int PointRecordId { get; set; }
    [Required, StringLength(1000, MinimumLength = 10)] public string Reason { get; set; } = string.Empty;
}

public sealed class ResolvePointAppealDto
{
    public bool Accept { get; set; }
    [Required, StringLength(1000, MinimumLength = 10)] public string Response { get; set; } = string.Empty;
}

public sealed class PointAppealDto
{
    public int Id { get; set; }
    public int PointRecordId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public int PeriodId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StaffResponse { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
