using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>
/// Score ledger entry: the training-point decision for one registration, at most one per registration.
/// A student's points in a period are the sum of Approved records; never store totals elsewhere.
/// </summary>
public class PointRecord
{
    [Key]
    public int Id { get; set; }

    public int RegistrationId { get; set; }

    [ForeignKey(nameof(RegistrationId))]
    public ActivityRegistration? Registration { get; set; }

    /// <summary>Denormalised from the registration so per-student and per-period queries stay simple.</summary>
    public int StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public User? Student { get; set; }

    public int ActivityId { get; set; }

    [ForeignKey(nameof(ActivityId))]
    public Activity? Activity { get; set; }

    public int PeriodId { get; set; }

    [ForeignKey(nameof(PeriodId))]
    public EvaluationPeriod? Period { get; set; }

    /// <summary>Points granted (the activity's TrainingPoints at approval time); counted only while Approved.</summary>
    public int Points { get; set; }

    /// <summary>One of <see cref="PointRecordStatus"/>.</summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = PointRecordStatus.Approved;

    [StringLength(500)]
    public string? Reason { get; set; }

    public int? DecidedById { get; set; }

    [ForeignKey(nameof(DecidedById))]
    public User? DecidedBy { get; set; }

    public DateTime DecidedAt { get; set; }

    public List<PointRecordHistory> History { get; set; } = [];
}

/// <summary>Append-only approval history of a <see cref="PointRecord"/> (US-22, US-23).</summary>
public class PointRecordHistory
{
    [Key]
    public int Id { get; set; }

    public int PointRecordId { get; set; }

    [ForeignKey(nameof(PointRecordId))]
    public PointRecord? PointRecord { get; set; }

    /// <summary>Null for the first decision.</summary>
    [StringLength(20)]
    public string? FromStatus { get; set; }

    [Required]
    [StringLength(20)]
    public string ToStatus { get; set; } = string.Empty;

    public int Points { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    public int? ChangedById { get; set; }

    [ForeignKey(nameof(ChangedById))]
    public User? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }
}

/// <summary>
/// Score decision states. US-22 creates Approved records; US-23 (Kiệt) moves records with a reason:
/// Approved → Revoked or Pending; Pending → Approved or Rejected; Rejected → Pending; Revoked is final.
/// </summary>
public static class PointRecordStatus
{
    public const string Approved = "Approved";
    public const string Pending = "Pending";
    public const string Rejected = "Rejected";
    public const string Revoked = "Revoked";

    public static readonly string[] All = [Approved, Pending, Rejected, Revoked];

    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [Approved] = [Revoked, Pending],
        [Pending] = [Approved, Rejected],
        [Rejected] = [Pending],
        [Revoked] = []
    };

    public static IReadOnlyList<string> NextOf(string current) =>
        Transitions.TryGetValue(current, out var next) ? next : [];
}
