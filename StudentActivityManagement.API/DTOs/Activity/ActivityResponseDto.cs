namespace StudentActivityManagement.API.DTOs.Activity
{
    public class ActivityResponseDto
    {
        public int Id { get; set; }
        public string ActivityName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Campus { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime RegistrationOpenTime { get; set; }
        public DateTime RegistrationCloseTime { get; set; }
        public int MaxParticipants { get; set; }
        public int CurrentParticipantsCount { get; set; }
        public int TrainingPoints { get; set; }
        public string TargetAudience { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRegistrationOpen => DateTime.UtcNow >= RegistrationOpenTime && DateTime.UtcNow <= RegistrationCloseTime && CurrentParticipantsCount < MaxParticipants;
    }
}
