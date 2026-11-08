using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;

namespace StudentActivityManagement.API.Tests;

public sealed class LoginLockoutTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public LoginLockoutTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task FiveWrongPasswordsLockTheAccountUntilItExpires()
    {
        var account = await CreateAccountAsync();
        var code = account.GetProperty("studentCode").GetString()!;
        var id = account.GetProperty("id").GetInt32();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await AccountTestClient.AssertProblemAsync(
                await _api.LoginResponseAsync(code, "wrong-password"),
                HttpStatusCode.Unauthorized, "invalid_credentials");
        }

        await AccountTestClient.AssertProblemAsync(
            await _api.LoginResponseAsync(code, AccountTestClient.StaffPassword),
            (HttpStatusCode)423, "account_temporarily_locked");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == id);
            user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        Assert.NotEmpty(await _api.LoginAsync(code, AccountTestClient.StaffPassword));
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Users.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(0, updated.FailedLoginCount);
        Assert.Null(updated.LockoutEndUtc);
    }

    [Fact]
    public async Task CorrectPasswordClearsAPartialFailureStreak()
    {
        var account = await CreateAccountAsync();
        var code = account.GetProperty("studentCode").GetString()!;
        for (var attempt = 0; attempt < 3; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await _api.LoginResponseAsync(code, "wrong-password")).StatusCode);

        await _api.LoginAsync(code, AccountTestClient.StaffPassword);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await _api.LoginResponseAsync(code, "wrong-password")).StatusCode);

        await AccountTestClient.AssertProblemAsync(
            await _api.LoginResponseAsync(code, AccountTestClient.StaffPassword),
            (HttpStatusCode)423, "account_temporarily_locked");
    }

    [Fact]
    public async Task OnlySystemAdminCanUnlockAnAccount()
    {
        var account = await CreateAccountAsync();
        var code = account.GetProperty("studentCode").GetString()!;
        var id = account.GetProperty("id").GetInt32();
        for (var attempt = 0; attempt < 5; attempt++)
            await _api.LoginResponseAsync(code, "wrong-password");

        var hssv = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        await AccountTestClient.AssertProblemAsync(
            await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/accounts/{id}/unlock", hssv, null),
            HttpStatusCode.Forbidden, "access_denied");

        var admin = await AdminTokenAsync();
        var response = await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/accounts/{id}/unlock", admin, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await _api.LoginAsync(code, AccountTestClient.StaffPassword));
    }

    [Fact]
    public async Task FourthRequestFromSameIpIsRateLimited()
    {
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LoginSecurity:RequestsPerMinutePerIp"] = "3"
            })));
        var api = new AccountTestClient(app.CreateClient());
        for (var attempt = 0; attempt < 3; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await api.LoginResponseAsync("unknown-account", "wrong-password")).StatusCode);

        await AccountTestClient.AssertProblemAsync(
            await api.LoginResponseAsync("unknown-account", "wrong-password"),
            HttpStatusCode.TooManyRequests, "too_many_login_attempts");
    }

    [Fact]
    public async Task ResponsesContainBasicSecurityHeaders()
    {
        var response = await _api.LoginResponseAsync("unknown-account", "wrong-password");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    private async Task<System.Text.Json.JsonElement> CreateAccountAsync()
    {
        var admin = await AdminTokenAsync();
        return await _api.CreateStaffAccountAsync(admin, "HSSV");
    }

    private Task<string> AdminTokenAsync() =>
        _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
}
