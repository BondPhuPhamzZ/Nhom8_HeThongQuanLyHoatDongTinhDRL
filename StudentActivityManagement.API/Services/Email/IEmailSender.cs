namespace StudentActivityManagement.API.Services.Email;

public interface IEmailSender
{
    bool IsAvailable { get; }
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}
