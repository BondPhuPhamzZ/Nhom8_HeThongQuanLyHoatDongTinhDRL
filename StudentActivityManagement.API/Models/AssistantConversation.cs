using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.Models;

public sealed class AssistantConversation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public int MessageCount { get; set; }
    public List<AssistantMessage> Messages { get; set; } = [];
}
