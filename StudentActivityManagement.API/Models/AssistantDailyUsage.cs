using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>
/// Questions one student asked the AI assistant on one day (Vietnam date). Kept in the database, not in memory,
/// so the daily limit survives restarts and protects the shared free Gemini quota (US-32).
/// </summary>
public class AssistantDailyUsage
{
    [Key]
    public int Id { get; set; }

    public int StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public User? Student { get; set; }

    /// <summary>Calendar day in Vietnam time (UTC+7).</summary>
    public DateOnly UsageDate { get; set; }

    public int QuestionCount { get; set; }
}
