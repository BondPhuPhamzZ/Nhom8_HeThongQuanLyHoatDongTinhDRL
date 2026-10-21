using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

public sealed class ApiContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiContractTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task StudentAndAdminAuthorizationFlow_ReturnsExpectedStatuses()
    {
        var studentToken = await LoginAsync("SV210002", "123456");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var meResponse = await _client.GetAsync("/api/v1/auth/me");
        var activitiesResponse = await _client.GetAsync("/api/v1/student-activities");
        var forbiddenResponse = await _client.GetAsync("/api/v1/admin/activities");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activitiesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal("application/problem+json", forbiddenResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("access_denied", await ReadStringAsync(forbiddenResponse, "errorCode"));

        var adminToken = await LoginAsync("ADMIN001", "Admin123@");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var adminResponse = await _client.GetAsync("/api/v1/admin/activities");

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidLoginBody_ReturnsValidationProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            usernameOrEmail = "",
            password = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("validation_failed", await ReadStringAsync(response, "errorCode"));
    }

    [Fact]
    public async Task OpenApi_OnlySecuredOperationsDeclareBearerAnd403()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        var login = paths.GetProperty("/api/v1/auth/login").GetProperty("post");
        var adminActivities = paths.GetProperty("/api/v1/admin/activities").GetProperty("get");

        Assert.False(login.TryGetProperty("security", out _));
        Assert.True(adminActivities.TryGetProperty("security", out _));
        Assert.True(adminActivities.GetProperty("responses").TryGetProperty("403", out _));
    }

    private async Task<string> LoginAsync(string usernameOrEmail, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            usernameOrEmail,
            password
        });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response did not contain a token.");
    }

    private static async Task<string?> ReadStringAsync(HttpResponseMessage response, string propertyName)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(propertyName).GetString();
    }
}
