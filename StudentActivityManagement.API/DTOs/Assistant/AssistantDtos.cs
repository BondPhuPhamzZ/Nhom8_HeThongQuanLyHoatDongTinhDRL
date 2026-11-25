using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StudentActivityManagement.API.DTOs.Assistant;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AssistantChatRequestDto
{
    [Range(1, int.MaxValue)]
    public int? ConversationId { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;
}

public record AssistantConversationDto(int Id, string Title, DateTime CreatedAt, DateTime LastMessageAt, int MessageCount);

public sealed record AssistantConversationDetailDto(
    int Id, string Title, DateTime CreatedAt, DateTime LastMessageAt, int MessageCount, List<AssistantMessageDto> Messages)
    : AssistantConversationDto(Id, Title, CreatedAt, LastMessageAt, MessageCount);

public sealed record AssistantMessageDto(
    int Id, string Role, string Content, DateTime CreatedAt, string Status,
    List<string> ToolsUsed, List<AssistantActivityDto> Activities, bool PersonalDataRedacted);

public sealed class AssistantReplyDto
{
    /// <summary>Plain-text answer in Vietnamese, without the <c>[#id]</c> activity markers.</summary>
    public string Reply { get; set; } = string.Empty;

    /// <summary>Activities the answer refers to, verified against this question's tool results.</summary>
    public List<AssistantActivityDto> Activities { get; set; } = [];

    /// <summary>Tools the answer is based on, so clients can show what was looked up.</summary>
    public List<string> ToolsUsed { get; set; } = [];

    /// <summary>Activity ids the model mentioned that no tool returned; they are removed from the answer.</summary>
    public int DroppedActivityReferences { get; set; }

    public string Model { get; set; } = string.Empty;

    public int ConversationId { get; set; }

    public int MessageId { get; set; }

    /// <summary>True when personal data (name, student code, email, phone, ID number) was removed before calling Gemini.</summary>
    public bool PersonalDataRedacted { get; set; }

    /// <summary>Questions the student may still ask today.</summary>
    public int RemainingToday { get; set; }
    public int DailyTokenLimit { get; set; }
    public int TokensUsedToday { get; set; }
}

public sealed class AssistantStatusDto
{
    /// <summary>False when the assistant is switched off or the server has no Gemini key; clients then hide the chat.</summary>
    public bool Enabled { get; set; }

    public string? Model { get; set; }

    public int RequestsPerMinute { get; set; }

    public int DailyQuestionLimit { get; set; }

    public int RemainingToday { get; set; }
    public int DailyTokenLimit { get; set; }
    public int TokensUsedToday { get; set; }
}

/// <summary>An activity the assistant referred to; built from tool results, so it always exists and the student may see it.</summary>
public sealed class AssistantActivityDto
{
    public int Id { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public int TrainingPoints { get; set; }
    public string? Campus { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? RegistrationCloseTime { get; set; }
    public bool? IsRegistrationOpen { get; set; }
    public string? MyRegistrationStatus { get; set; }
}
