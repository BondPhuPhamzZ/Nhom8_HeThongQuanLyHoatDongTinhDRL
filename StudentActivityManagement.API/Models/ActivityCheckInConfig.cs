using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>
/// Check-in settings of one activity (US-26). No row means the defaults in SettingDefaults apply;
/// read through ICheckInConfigProvider rather than this table directly.
/// </summary>
public class ActivityCheckInConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int ActivityId { get; set; }

    [ForeignKey(nameof(ActivityId))]
    public Activity? Activity { get; set; }

    [Required]
    [StringLength(20)]
    public string Method { get; set; } = CheckInMethods.Code;

    /// <summary>Check-in opens this many minutes before each session starts.</summary>
    public int OpenBeforeMinutes { get; set; }

    /// <summary>Check-in closes this many minutes after each session ends.</summary>
    public int CloseAfterMinutes { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? UpdatedById { get; set; }

    [ForeignKey(nameof(UpdatedById))]
    public User? UpdatedBy { get; set; }
}
