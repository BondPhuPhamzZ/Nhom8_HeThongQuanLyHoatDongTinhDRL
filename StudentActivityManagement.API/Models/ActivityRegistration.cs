using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models
{
    public class ActivityRegistration
    {
        [Key]
        public int Id { get; set; }

        public int ActivityId { get; set; }

        [ForeignKey("ActivityId")]
        public virtual Activity? Activity { get; set; }

        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual User? Student { get; set; }

        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        [StringLength(30)]
        public string Status { get; set; } = "Registered"; // Registered, Attended, Cancelled, Absent

        public string EvidenceUrl { get; set; } = string.Empty;

        public int PointsEarned { get; set; } = 0;

        public DateTime? CheckInTime { get; set; }
    }
}
