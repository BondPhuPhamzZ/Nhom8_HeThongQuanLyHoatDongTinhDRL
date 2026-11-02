using StudentActivityManagement.API.DTOs.Activity;

namespace StudentActivityManagement.API.DTOs.Student;

public sealed class StudentPointsRecommendationDto
{
    public EvaluationPeriodSummaryDto Period { get; set; } = new();
    public int ApprovedPoints { get; set; }
    public int MissingPoints { get; set; }
    public bool IsAtRisk { get; set; }
    public List<ActivityResponseDto> RecommendedActivities { get; set; } = [];
}

public sealed class EvaluationPeriodSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TargetPoints { get; set; }
    public string Status { get; set; } = string.Empty;
}
