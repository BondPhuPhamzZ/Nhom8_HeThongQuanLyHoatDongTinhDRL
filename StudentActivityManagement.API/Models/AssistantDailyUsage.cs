using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>
/// AI usage by account, feature and Vietnam calendar day. Kept in the database, not in memory,
/// so the daily limit survives restarts and protects the shared free Gemini quota (US-32).
/// </summary>
public class AssistantDailyUsage
{
    [Key]
    public int Id { get; set; }

    public const string Assistant = "Assistant";
    public const string StaffAi = "StaffAi";

    /// <summary>Holds any user id (student or staff).</summary>
    public int StudentId { get; set; }

    [StringLength(20)]
    public string Feature { get; set; } = Assistant;

    public int TokenCount { get; set; }

    [ForeignKey(nameof(StudentId))]
    public User? Student { get; set; }

    /// <summary>Calendar day in Vietnam time (UTC+7).</summary>
    public DateOnly UsageDate { get; set; }

    public int QuestionCount { get; set; }
}
