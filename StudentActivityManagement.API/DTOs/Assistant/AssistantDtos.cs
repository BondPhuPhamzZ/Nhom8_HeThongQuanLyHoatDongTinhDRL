namespace StudentActivityManagement.API.DTOs.Assistant;

/// <summary>
/// One question to the assistant. The client keeps the conversation and sends it back each time (the API stores
/// nothing); only <c>user</c> and <c>assistant</c> text turns are accepted and the last one must be the new question.
/// </summary>
public sealed class AssistantChatRequestDto
{
    public List<AssistantChatMessageDto> Messages { get; set; } = [];
}

public sealed class AssistantChatMessageDto
{
    /// <summary><c>user</c> or <c>assistant</c>.</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

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
}

public sealed class AssistantStatusDto
{
    /// <summary>False when the assistant is switched off or the server has no Gemini key; clients then hide the chat.</summary>
    public bool Enabled { get; set; }

    public string? Model { get; set; }

    public int RequestsPerMinute { get; set; }
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
