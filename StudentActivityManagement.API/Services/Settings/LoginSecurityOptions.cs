namespace StudentActivityManagement.API.Services.Settings;

public sealed class LoginSecurityOptions
{
    public const string SectionName = "LoginSecurity";

    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int RequestsPerMinutePerIp { get; set; } = 10;
}
