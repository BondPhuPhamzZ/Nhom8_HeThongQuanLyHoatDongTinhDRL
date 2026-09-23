using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StudentActivityManagement.API.Models
{
    public class Activity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string ActivityName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [StringLength(200)]
        public string Location { get; set; } = string.Empty;

        [StringLength(50)]
        public string Campus { get; set; } = "Cơ sở 1";

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public DateTime RegistrationOpenTime { get; set; }

        public DateTime RegistrationCloseTime { get; set; }

        public int MaxParticipants { get; set; } = 50;

        public int CurrentParticipantsCount { get; set; } = 0;

        public int TrainingPoints { get; set; } = 5;

        [StringLength(100)]
        public string TargetAudience { get; set; } = "Tất cả sinh viên";

        [StringLength(30)]
        public string Status { get; set; } = "Published"; // Draft, Published, Ongoing, Completed, Cancelled

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [JsonIgnore]
        public virtual ICollection<ActivityRegistration> Registrations { get; set; } = new List<ActivityRegistration>();
    }
}
