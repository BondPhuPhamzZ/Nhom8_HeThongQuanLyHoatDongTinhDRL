using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace StudentActivityManagement.API.DTOs.Evidence;

public sealed class EvidenceUploadDto            // [FromForm]
{
    [Required] public string EvidenceType { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
    [StringLength(100, MinimumLength = 4)] public string? ConfirmationCode { get; set; }
    public int? SessionId { get; set; }
    [StringLength(500)] public string? Note { get; set; }
}

public sealed class EvidenceResponseDto
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public int? SessionId { get; set; }
    public string? SessionName { get; set; }
    public string EvidenceType { get; set; } = string.Empty;
    public string? ConfirmationCode { get; set; }
    public string? Note { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? FileUrl { get; set; }          // "/api/v1/student-activities/{a}/evidences/{id}/file" hoac null
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public DateTime UploadedAt { get; set; }      // UTC, JSON dang ...Z
    public bool CanDelete { get; set; }           // Pending va con han nop
}
