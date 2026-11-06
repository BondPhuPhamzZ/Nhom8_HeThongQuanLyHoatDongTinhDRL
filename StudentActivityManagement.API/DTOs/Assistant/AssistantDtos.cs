namespace StudentActivityManagement.API.DTOs.Assistant;

/// <summary>An activity the assistant referred to; built from tool results, so it always exists and the student may see it.</summary>
public sealed class AssistantActivityDto
{
    public int Id { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public int TrainingPoints { get; set; }
    public string? Campus { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? RegistrationCloseTime { get; set; }
    public bool? IsRegistrationOpen { get; set; }
    public string? MyRegistrationStatus { get; set; }
}
