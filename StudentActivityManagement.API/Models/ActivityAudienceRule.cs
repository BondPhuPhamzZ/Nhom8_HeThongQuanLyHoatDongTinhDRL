using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace StudentActivityManagement.API.Models;

public class ActivityAudienceRule
{
    [Key]
    public int Id { get; set; }

    public int ActivityId { get; set; }

    [ForeignKey(nameof(ActivityId))]
    [JsonIgnore]
    public Activity? Activity { get; set; }

    [StringLength(50)]
    public string? Campus { get; set; }

    [StringLength(50)]
    public string? AcademicYear { get; set; }

    public int? ClassId { get; set; }

    [ForeignKey(nameof(ClassId))]
    public Class? Class { get; set; }

    [Required]
    [StringLength(20)]
    public string ParticipationType { get; set; } = AudienceParticipationType.Optional;
}

public static class AudienceParticipationType
{
    public const string Required = "Required";
    public const string Recommended = "Recommended";
    public const string Optional = "Optional";

    public static readonly string[] All = [Required, Recommended, Optional];
}
