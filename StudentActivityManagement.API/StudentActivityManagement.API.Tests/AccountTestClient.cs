using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// Gửi request với token riêng cho từng lời gọi để nhiều tài khoản dùng chung một HttpClient an toàn.
/// </summary>
internal sealed class AccountTestClient
{
    public const string SystemAdminCode = "ADMIN001";
    public const string SystemAdminPassword = "Admin123@";
    public const string HssvCode = "HSSV001";
    public const string HssvPassword = "Hssv123@";
    public const string StaffPassword = "Staff123@";

    private readonly HttpClient _client;

    public AccountTestClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<string> LoginAsync(string usernameOrEmail, string password)
    {
        var response = await LoginResponseAsync(usernameOrEmail, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response did not contain a token.");
    }

    public Task<HttpResponseMessage> LoginResponseAsync(string usernameOrEmail, string password) =>
        SendAsync(HttpMethod.Post, "/api/v1/auth/login", null, new { usernameOrEmail, password });

    public Task<HttpResponseMessage> GetAsync(string url, string? token) =>
        SendAsync(HttpMethod.Get, url, token, null);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    public async Task<JsonElement> CreateStaffAccountAsync(string adminToken, string role, string? nameTag = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/accounts", adminToken, new
        {
            studentCode = $"CB{suffix}",
            fullName = $"Cán bộ kiểm thử {nameTag ?? suffix}",
            email = $"cb{suffix}@school.edu.vn",
            password = StaffPassword,
            phone = "0900000001",
            role
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("data").Clone();
    }

    public Task<HttpResponseMessage> PatchActiveStatusAsync(string token, int accountId, bool isActive) =>
        SendAsync(HttpMethod.Patch, $"/api/v1/admin/accounts/{accountId}/active-status", token, new { isActive });

    public Task<HttpResponseMessage> PatchRoleAsync(string token, int accountId, string role) =>
        SendAsync(HttpMethod.Patch, $"/api/v1/admin/accounts/{accountId}/role", token, new { role });

    public async Task<int> GetCurrentUserIdAsync(string token)
    {
        var response = await GetAsync("/api/v1/auth/me", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetInt32();
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(response);
        Assert.Equal(errorCode, problem.GetProperty("errorCode").GetString());
    }
}
