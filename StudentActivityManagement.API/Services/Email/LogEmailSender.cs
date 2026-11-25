namespace StudentActivityManagement.API.Services.Email;

public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public bool IsAvailable => true;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("Development email to {Recipient}: {Subject}. {EmailBody}", to, subject, body);
        return Task.CompletedTask;
    }
}
