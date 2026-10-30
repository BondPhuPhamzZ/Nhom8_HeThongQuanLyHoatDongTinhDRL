using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.Models;

/// <summary>
/// Training-point evaluation period (e.g. a semester). An activity's points belong to the period that
/// contains the activity's end time; points are only recorded while that period is Open.
/// </summary>
public class EvaluationPeriod
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>Points a student is expected to reach in the period; used for the at-risk check (US-11, US-24).</summary>
    public int TargetPoints { get; set; }

    /// <summary>One of <see cref="EvaluationPeriodStatus"/>.</summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = EvaluationPeriodStatus.Open;

    public DateTime CreatedAt { get; set; }
}

public static class EvaluationPeriodStatus
{
    public const string Open = "Open";
    public const string Closed = "Closed";

    public static readonly string[] All = [Open, Closed];
}
