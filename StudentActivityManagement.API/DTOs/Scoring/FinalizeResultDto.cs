namespace StudentActivityManagement.API.DTOs.Scoring;

public sealed record FinalizeResultDto(int ApprovedCount, int AbsentCount, int SkippedExistingCount, DateTime PointsFinalizedAt);
