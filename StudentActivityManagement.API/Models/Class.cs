using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace StudentActivityManagement.API.Models
{
    public class Class
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ClassCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ClassName { get; set; } = string.Empty;

        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        public int? MonitorStudentId { get; set; }

        [ForeignKey("MonitorStudentId")]
        [JsonIgnore]
        public virtual User? MonitorStudent { get; set; }

        [JsonIgnore]
        public virtual ICollection<User> Students { get; set; } = new List<User>();
    }
}
