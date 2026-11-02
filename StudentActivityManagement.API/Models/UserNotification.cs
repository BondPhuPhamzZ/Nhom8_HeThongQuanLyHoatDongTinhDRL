using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>An in-app message; SourceKey makes a reminder unique per session, offset and student.</summary>
public sealed class UserNotification
{
    [Key]
    public int Id { get; set; }

    public int RecipientUserId { get; set; }

    [ForeignKey(nameof(RecipientUserId))]
    public User? Recipient { get; set; }

    [Required, StringLength(120)]
    public string SourceKey { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Kind { get; set; } = "ActivityReminder";

    public int ActivityId { get; set; }
    public int SessionId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
