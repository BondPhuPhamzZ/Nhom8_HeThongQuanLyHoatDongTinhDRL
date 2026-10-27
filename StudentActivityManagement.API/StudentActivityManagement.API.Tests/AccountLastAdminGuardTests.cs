using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// US-25: hai SystemAdmin khóa nhau cùng lúc không được làm hệ thống mất Quản trị viên hệ thống cuối cùng.
/// Class riêng vì test khóa tài khoản seed ADMIN001 (mỗi fixture có database riêng).
/// </summary>
public sealed class AccountLastAdminGuardTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public AccountLastAdminGuardTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task ConcurrentMutualLock_KeepsAtLeastOneActiveSystemAdmin()
    {
        var seedAdminToken = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var seedAdminId = await _api.GetCurrentUserIdAsync(seedAdminToken);
        var secondAdmin = await _api.CreateStaffAccountAsync(seedAdminToken, "SystemAdmin");
        var secondAdminId = secondAdmin.GetProperty("id").GetInt32();
        var secondAdminToken = await _api.LoginAsync(
            secondAdmin.GetProperty("studentCode").GetString()!, AccountTestClient.StaffPassword);

        var responses = await Task.WhenAll(
            _api.PatchActiveStatusAsync(seedAdminToken, secondAdminId, false),
            _api.PatchActiveStatusAsync(secondAdminToken, seedAdminId, false));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Single(response => response.StatusCode != HttpStatusCode.OK);
        Assert.Contains(rejected.StatusCode, new[]
        {
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.Conflict
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activeAdmins = await db.Users
                .Where(user => user.Role == "SystemAdmin" && user.IsActive)
                .Select(user => user.Id)
                .ToListAsync();
            Assert.Single(activeAdmins);
        }

        var survivorIsSeed = responses[0].StatusCode == HttpStatusCode.OK;
        var survivorToken = survivorIsSeed ? seedAdminToken : secondAdminToken;
        var survivorId = survivorIsSeed ? seedAdminId : secondAdminId;
        await AccountTestClient.AssertProblemAsync(
            await _api.PatchActiveStatusAsync(survivorToken, survivorId, false),
            HttpStatusCode.Conflict,
            "business_rule_violation");
    }
}
