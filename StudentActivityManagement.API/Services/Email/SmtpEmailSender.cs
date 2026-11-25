using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace StudentActivityManagement.API.Services.Email;

public sealed class SmtpEmailOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public bool EnableSsl { get; set; } = true;
}

public sealed class SmtpEmailSender(IOptions<SmtpEmailOptions> options) : IEmailSender
{
    private SmtpEmailOptions Settings => options.Value;

    public bool IsAvailable => !string.IsNullOrWhiteSpace(Settings.Host) && !string.IsNullOrWhiteSpace(Settings.From);

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        using var message = new MailMessage(Settings.From!, to, subject, body)
        {
            SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8
        };
        using var client = new SmtpClient(Settings.Host!, Settings.Port) { EnableSsl = Settings.EnableSsl };
        if (!string.IsNullOrEmpty(Settings.UserName))
            client.Credentials = new NetworkCredential(Settings.UserName, Settings.Password);
        await client.SendMailAsync(message, ct);
    }
}
