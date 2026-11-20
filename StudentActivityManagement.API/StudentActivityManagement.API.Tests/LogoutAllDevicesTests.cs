using System.Net;

namespace StudentActivityManagement.API.Tests;

/// <summary>US-44: logging out in one place signs the account out on every browser and device.</summary>
public sealed class LogoutAllDevicesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly AccountTestClient _api;

    public LogoutAllDevicesTests(CustomWebApplicationFactory factory)
    {
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task LogoutOnOneDevice_RevokesTheTokensOfEveryOtherDevice()
    {
        // Same account signed in on two browsers/devices.
        var browserA = await _api.LoginAsync("SV210002", "123456");
        var browserB = await _api.LoginAsync("SV210002", "123456");
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/auth/me", browserA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/auth/me", browserB)).StatusCode);

        var logout = await _api.SendAsync(HttpMethod.Post, "/api/v1/auth/logout", browserA, null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        // The token used to log out and the one on the other device are both refused, on any endpoint.
        await AccountTestClient.AssertProblemAsync(await _api.GetAsync("/api/v1/auth/me", browserA),
            HttpStatusCode.Unauthorized, "authentication_required");
        await AccountTestClient.AssertProblemAsync(await _api.GetAsync("/api/v1/auth/me", browserB),
            HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync("/api/v1/student/point-appeals", browserB)).StatusCode);

        // Signing in again gives a working token; other accounts were never affected.
        var again = await _api.LoginAsync("SV210002", "123456");
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/auth/me", again)).StatusCode);
        var monitor = await _api.LoginAsync("SV210001", "123456");
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/auth/me", monitor)).StatusCode);
    }

    [Fact]
    public async Task Logout_RequiresASignedInUser()
    {
        await AccountTestClient.AssertProblemAsync(await _api.SendAsync(HttpMethod.Post, "/api/v1/auth/logout", null, null),
            HttpStatusCode.Unauthorized, "authentication_required");
    }
}
