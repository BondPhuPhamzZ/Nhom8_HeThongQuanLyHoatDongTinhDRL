using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.Models;

public sealed class AssistantMessage
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
    public const string Answered = "Answered";
    public const string Failed = "Failed";

    public int Id { get; set; }
    public int ConversationId { get; set; }
    public AssistantConversation? Conversation { get; set; }

    [StringLength(10)]
    public string Role { get; set; } = string.Empty;

    [StringLength(8000)]
    public string Content { get; set; } = string.Empty;

    [StringLength(500)]
    public string ToolsUsed { get; set; } = string.Empty;

    public string? ActivitiesJson { get; set; }
    public int PromptTokens { get; set; }
    public int OutputTokens { get; set; }

    [StringLength(60)]
    public string Model { get; set; } = string.Empty;

    [StringLength(10)]
    public string Status { get; set; } = Answered;

    public bool PersonalDataRedacted { get; set; }
    public DateTime CreatedAt { get; set; }
}
