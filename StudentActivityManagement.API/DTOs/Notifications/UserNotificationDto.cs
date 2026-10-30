namespace StudentActivityManagement.API.DTOs.Notifications;

public sealed class UserNotificationDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int ActivityId { get; set; }
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
