using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services.Email;

namespace StudentActivityManagement.API.Tests;

public sealed class PasswordResetTests
{
    private const string Student = "SV210002";
    private const string SeedPassword = "123456";
    private const string ResetPassword = "ResetDemo2026";
    private const string ForgotPath = "/api/v1/auth/forgot-password";
    private const string ResetPath = "/api/v1/auth/reset-password";

    [Fact]
    public async Task Forgot_UsesTheSameReplyAndStoresOnlyAHash()
    {
        using var factory = new CustomWebApplicationFactory();
        var mail = new CapturingEmailSender();
        using var app = WithEmail(factory, mail);
        var api = new AccountTestClient(app.CreateClient());
        var missing = await Forgot(api, "no-account");
        var found = await Forgot(api, Student);
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Equal(missing.StatusCode, found.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await found.Content.ReadAsStringAsync());
        var code = Assert.Single(mail.Codes);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reset = await db.PasswordResetCodes.AsNoTracking().SingleAsync();
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))), reset.CodeHash);
        Assert.Equal(TimeSpan.FromMinutes(10), reset.ExpiresAt - reset.CreatedAt);
        Assert.Null(reset.UsedAt);
    }

    [Fact]
    public async Task Reset_RevokesOldTokensAndRejectsCodeReuse()
    {
        using var factory = new CustomWebApplicationFactory();
        var mail = new CapturingEmailSender();
        using var app = WithEmail(factory, mail);
        var api = new AccountTestClient(app.CreateClient());
        var previous = await api.LoginAsync(Student, SeedPassword);
        Assert.Equal(HttpStatusCode.OK, (await Forgot(api, Student)).StatusCode);
        var code = Assert.Single(mail.Codes);
        Assert.Equal(HttpStatusCode.OK, (await Reset(api, code)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/v1/auth/me", previous)).StatusCode);
        Assert.NotEmpty(await api.LoginAsync(Student, ResetPassword));
        await AssertInvalidCode(await Reset(api, code));
    }

    [Fact]
    public async Task Reset_ExhaustionAndExpiryAreEnforcedAndFourthCodeIsQuietlySuppressed()
    {
        using var factory = new CustomWebApplicationFactory();
        var mail = new CapturingEmailSender();
        using var app = WithEmail(factory, mail);
        var api = new AccountTestClient(app.CreateClient());
        Assert.Equal(HttpStatusCode.OK, (await Forgot(api, Student)).StatusCode);
        var code = Assert.Single(mail.Codes);
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++) await AssertInvalidCode(await Reset(api, wrong));
        await AssertInvalidCode(await Reset(api, code));

        Assert.Equal(HttpStatusCode.OK, (await Forgot(api, Student)).StatusCode);
        factory.Time.Advance(TimeSpan.FromMinutes(11));
        await AssertInvalidCode(await Reset(api, mail.Codes[^1]));

        Assert.Equal(HttpStatusCode.OK, (await Forgot(api, Student)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Forgot(api, Student)).StatusCode);
        Assert.Equal(3, mail.Codes.Count);
    }

    [Fact]
    public async Task Reset_AfterLoginLockout_ClearsTheLock()
    {
        using var factory = new CustomWebApplicationFactory();
        var mail = new CapturingEmailSender();
        using var app = WithEmail(factory, mail);
        var api = new AccountTestClient(app.CreateClient());
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await api.LoginResponseAsync(Student, "WrongPassword")).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await api.LoginResponseAsync(Student, SeedPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Forgot(api, Student)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Reset(api, Assert.Single(mail.Codes))).StatusCode);
        Assert.NotEmpty(await api.LoginAsync(Student, ResetPassword));
    }

    [Fact]
    public async Task AdminTemporaryPassword_RestrictsRoutesUntilChange()
    {
        using var factory = new CustomWebApplicationFactory();
        using var app = WithEmail(factory, new CapturingEmailSender());
        var api = new AccountTestClient(app.CreateClient());
        var staff = await api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var admin = await api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var old = await api.LoginAsync(Student, SeedPassword);
        var id = await api.GetCurrentUserIdAsync(old);
        var route = $"/api/v1/admin/accounts/{id}/reset-password";

        await AccountTestClient.AssertProblemAsync(await api.SendAsync(HttpMethod.Post, route, staff, null),
            HttpStatusCode.Forbidden, ApiErrorCodes.AccessDenied);
        var result = await api.SendAsync(HttpMethod.Post, route, admin, null);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Headers.CacheControl?.NoStore);
        var temp = (await AccountTestClient.ReadJsonAsync(result)).GetProperty("data").GetProperty("temporaryPassword").GetString()!;
        Assert.Equal(10, temp.Length);
        Assert.Matches("[A-Z]", temp);
        Assert.Matches("[a-z]", temp);
        Assert.Matches("[2-9]", temp);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/v1/auth/me", old)).StatusCode);
        var login = (await AccountTestClient.ReadJsonAsync(await api.LoginResponseAsync(Student, temp))).GetProperty("data");
        Assert.True(login.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        var restricted = login.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/v1/auth/me", restricted)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/v1/me/profile", restricted)).StatusCode);
        await AccountTestClient.AssertProblemAsync(await api.GetAsync("/api/v1/student-activities", restricted),
            HttpStatusCode.Forbidden, ApiErrorCodes.PasswordChangeRequired);

        var change = await api.SendAsync(HttpMethod.Post, "/api/v1/me/change-password", restricted,
            new { currentPassword = temp, newPassword = ResetPassword });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var session = (await AccountTestClient.ReadJsonAsync(change)).GetProperty("data");
        Assert.False(session.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/v1/auth/me", restricted)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/v1/student-activities", session.GetProperty("token").GetString())).StatusCode);
    }

    [Fact]
    public async Task StaffPasswordUpdate_RequiresTheStudentToChangeTheIssuedPassword()
    {
        using var app = new CustomWebApplicationFactory();
        var api = new AccountTestClient(app.CreateClient());
        var staff = await api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var old = await api.LoginAsync(Student, SeedPassword);
        var id = await api.GetCurrentUserIdAsync(old);
        var me = (await AccountTestClient.ReadJsonAsync(await api.GetAsync("/api/v1/auth/me", old))).GetProperty("data");
        var updated = await api.SendAsync(HttpMethod.Put, $"/api/v1/admin/students/{id}", staff, new
        {
            fullName = me.GetProperty("fullName").GetString(),
            email = me.GetProperty("email").GetString(),
            phone = me.GetProperty("phone").GetString(),
            campus = me.GetProperty("campus").GetString(),
            academicYear = me.GetProperty("academicYear").GetString(),
            classId = me.GetProperty("classId").GetInt32(),
            password = ResetPassword
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var next = await api.LoginAsync(Student, ResetPassword);
        await AccountTestClient.AssertProblemAsync(await api.GetAsync("/api/v1/student-activities", next),
            HttpStatusCode.Forbidden, ApiErrorCodes.PasswordChangeRequired);
    }

    [Fact]
    public async Task Forgot_WithoutAnAvailableMailSender_Returns503()
    {
        using var factory = new CustomWebApplicationFactory();
        using var app = WithEmail(factory, new UnavailableEmailSender());
        var api = new AccountTestClient(app.CreateClient());
        await AccountTestClient.AssertProblemAsync(await Forgot(api, Student),
            HttpStatusCode.ServiceUnavailable, ApiErrorCodes.ServiceUnavailable);
    }

    private static WebApplicationFactory<Program> WithEmail(CustomWebApplicationFactory factory, IEmailSender sender) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(sender)));

    private static Task<HttpResponseMessage> Forgot(AccountTestClient api, string login) =>
        api.SendAsync(HttpMethod.Post, ForgotPath, null, new { login });

    private static Task<HttpResponseMessage> Reset(AccountTestClient api, string code) =>
        api.SendAsync(HttpMethod.Post, ResetPath, null, new { login = Student, code, newPassword = ResetPassword });

    private static async Task AssertInvalidCode(HttpResponseMessage response)
    {
        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("Mã xác nhận không đúng hoặc đã hết hạn.", (await AccountTestClient.ReadJsonAsync(response)).GetProperty("detail").GetString());
    }

    private sealed class UnavailableEmailSender : IEmailSender
    {
        public bool IsAvailable => false;

        public Task SendAsync(string to, string subject, string body, CancellationToken ct) =>
            throw new InvalidOperationException("An unavailable mail sender must not be called.");
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public bool IsAvailable => true;
        public List<string> Codes { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken ct)
        {
            Codes.Add(Regex.Match(body, @"\b[0-9]{6}\b").Value);
            return Task.CompletedTask;
        }
    }
}
