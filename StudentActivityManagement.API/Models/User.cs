using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace StudentActivityManagement.API.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string StudentCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = Models.Role.Student;

        [StringLength(50)]
        public string Campus { get; set; } = "Cơ sở 1";

        [StringLength(50)]
        public string AcademicYear { get; set; } = "K15";

        public int? ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class? Class { get; set; }

        public bool IsClassMonitor { get; set; } = false;

        public int AccumulatedPoints { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [JsonIgnore]
        public virtual ICollection<ActivityRegistration> Registrations { get; set; } = new List<ActivityRegistration>();
    }
}
