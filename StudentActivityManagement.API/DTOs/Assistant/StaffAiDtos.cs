namespace StudentActivityManagement.API.DTOs.Assistant;

/// <summary>US-33: AI commentary on a period report. Every statement passed the number check against the report data.</summary>
public sealed class ReportInsightsDto
{
    public string PeriodName { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = [];
    public List<string> Concerns { get; set; } = [];
    public List<string> Suggestions { get; set; } = [];

    /// <summary>Statements removed because they contained a number that is not in the report data.</summary>
    public int RemovedStatements { get; set; }

    public string Model { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

/// <summary>US-34: AI draft of an official answer to an escalated class question; staff edit it before sending.</summary>
public sealed class EscalationDraftDto
{
    public int EscalationId { get; set; }
    public string Draft { get; set; } = string.Empty;

    /// <summary>high, medium or low: how well the system rules cover the question.</summary>
    public string Confidence { get; set; } = "low";

    /// <summary>What staff should check or add before sending (the rules did not say).</summary>
    public List<string> MissingInformation { get; set; } = [];

    /// <summary>True when personal data in the question was hidden before calling Gemini.</summary>
    public bool PersonalDataRedacted { get; set; }

    public string Model { get; set; } = string.Empty;
}
