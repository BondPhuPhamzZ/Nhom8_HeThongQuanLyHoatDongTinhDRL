using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>
/// One reminder moment of an activity, in minutes before each session starts (US-26).
/// Sprint 3 only stores the schedule; sending reminders is US-06.
/// </summary>
public class ActivityReminderOffset
{
    [Key]
    public int Id { get; set; }

    public int ActivityId { get; set; }

    [ForeignKey(nameof(ActivityId))]
    public Activity? Activity { get; set; }

    public int OffsetMinutes { get; set; }
}
