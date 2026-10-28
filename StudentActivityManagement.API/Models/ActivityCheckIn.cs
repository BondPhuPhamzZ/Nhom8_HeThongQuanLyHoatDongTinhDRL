using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>Attendance of one registration at one session; at most one row per (registration, session).</summary>
public class ActivityCheckIn
{
    [Key]
    public int Id { get; set; }

    public int RegistrationId { get; set; }

    [ForeignKey(nameof(RegistrationId))]
    public ActivityRegistration? Registration { get; set; }

    public int SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public ActivitySession? Session { get; set; }

    public DateTime CheckedInAt { get; set; }

    /// <summary>One of <see cref="CheckInMethods"/>: how this row was recorded.</summary>
    [Required]
    [StringLength(20)]
    public string Method { get; set; } = CheckInMethods.Code;

    /// <summary>Staff member who recorded a manual check-in; null when the student checked in.</summary>
    public int? RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public User? RecordedBy { get; set; }
}
