namespace StudentActivityManagement.API.DTOs.Activity
{
    public class ActivityResponseDto
    {
        public int Id { get; set; }
        public string ActivityName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OrganizerName { get; set; } = string.Empty;
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
        public List<ActivitySessionResponseDto> Sessions { get; set; } = [];
        public List<ActivityAudienceRuleResponseDto> AudienceRules { get; set; } = [];
        public ActivityEligibilityDto? Eligibility { get; set; }
        /// <summary>Caller's own registration status (Registered, Cancelled, ...) on student endpoints; null when never registered or for staff.</summary>
        public string? MyRegistrationStatus { get; set; }
        public int RemainingSlots => Math.Max(0, MaxParticipants - CurrentParticipantsCount);
        public bool IsRegistrationOpen => DateTime.UtcNow >= RegistrationOpenTime && DateTime.UtcNow <= RegistrationCloseTime && CurrentParticipantsCount < MaxParticipants;
    }
}
