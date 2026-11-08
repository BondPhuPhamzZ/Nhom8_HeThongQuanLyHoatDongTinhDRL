using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.Models;

public sealed class PointAppeal
{
    public int Id { get; set; }
    public int PointRecordId { get; set; }
    public PointRecord? PointRecord { get; set; }
    public int StudentId { get; set; }
    public User? Student { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Status { get; set; } = PointAppealStatus.Pending;
    [StringLength(1000)] public string? StaffResponse { get; set; }
    public int? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public static class PointAppealStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
    public static readonly string[] All = [Pending, Accepted, Rejected, Withdrawn];
}
