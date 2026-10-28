using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>
/// Participation evidence submitted by a student (US-08) and reviewed by Phòng QL HSSV (US-21).
/// File evidence is stored by IEvidenceFileStorage under a server-generated name; never expose
/// <see cref="StoredFileName"/> to clients.
/// </summary>
public class ActivityEvidence
{
    [Key]
    public int Id { get; set; }

    public int RegistrationId { get; set; }

    [ForeignKey(nameof(RegistrationId))]
    public ActivityRegistration? Registration { get; set; }

    /// <summary>Optional session the evidence belongs to.</summary>
    public int? SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public ActivitySession? Session { get; set; }

    /// <summary>One of <see cref="EvidenceTypes"/>.</summary>
    [Required]
    [StringLength(30)]
    public string EvidenceType { get; set; } = EvidenceTypes.Image;

    [StringLength(100)]
    public string? ConfirmationCode { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [StringLength(255)]
    public string? OriginalFileName { get; set; }

    [StringLength(100)]
    public string? StoredFileName { get; set; }

    [StringLength(100)]
    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    /// <summary>One of <see cref="EvidenceReviewStatus"/>.</summary>
    [Required]
    [StringLength(30)]
    public string ReviewStatus { get; set; } = EvidenceReviewStatus.Pending;

    [StringLength(500)]
    public string? ReviewNote { get; set; }

    public int? ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public User? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime UploadedAt { get; set; }
}
