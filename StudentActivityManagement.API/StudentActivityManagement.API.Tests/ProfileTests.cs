using System.Net;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Tests;

public sealed class ProfileTests
{
    private const string Path = "/api/v1/me/profile";
    private const string ChangePath = "/api/v1/me/change-password";
    private const string SeedPassword = "123456";
    private const string NewPassword = "DemoChanged2026";

    [Fact]
    public async Task Update_NormalizesContactAndPreservesInstitutionalFields()
    {
        using var app = new CustomWebApplicationFactory();
        var api = new AccountTestClient(app.CreateClient());
        var student = await api.LoginAsync("SV210002", SeedPassword);
        var before = (await AccountTestClient.ReadJsonAsync(await api.GetAsync(Path, student))).GetProperty("data");
        var updated = await api.SendAsync(HttpMethod.Put, Path, student, new
        {
            email = "  PROFILE@EXAMPLE.TEST  ", phone = "+84901234567", currentPassword = SeedPassword,
            fullName = "Tên giả", studentCode = "Mã giả", campus = "Cơ sở giả", academicYear = "K99", classId = 999
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var data = (await AccountTestClient.ReadJsonAsync(updated)).GetProperty("data");
        foreach (var key in new[] { "fullName", "studentCode", "classCode", "campus", "academicYear" })
            Assert.Equal(before.GetProperty(key).GetString(), data.GetProperty(key).GetString());
        Assert.Equal("profile@example.test", data.GetProperty("email").GetString());
        Assert.Equal("+84901234567", data.GetProperty("phone").GetString());
        Assert.NotNull(data.GetProperty("updatedAt").GetString());
        var current = (await AccountTestClient.ReadJsonAsync(await api.GetAsync("/api/v1/auth/me", student))).GetProperty("data");
        Assert.Equal("profile@example.test", current.GetProperty("email").GetString());
        Assert.Equal("+84901234567", current.GetProperty("phone").GetString());
        var notifications = (await AccountTestClient.ReadJsonAsync(await api.GetAsync("/api/v1/notifications", student)))
            .GetProperty("data").GetProperty("items");
        Assert.Contains(notifications.EnumerateArray(), item => item.GetProperty("kind").GetString() == "ProfileUpdated");
    }

    [Fact]
    public async Task Update_RejectsMissingSessionWrongPasswordDuplicateEmailAndInvalidPhone()
    {
        using var app = new CustomWebApplicationFactory();
        var api = new AccountTestClient(app.CreateClient());
        var student = await api.LoginAsync("SV210002", SeedPassword);
        var monitor = await api.LoginAsync("SV210001", SeedPassword);
        var mine = (await AccountTestClient.ReadJsonAsync(await api.GetAsync(Path, monitor))).GetProperty("data");
        var duplicate = mine.GetProperty("email").GetString()!.ToUpperInvariant();

        Task<HttpResponseMessage> Put(string? token, string email, string phone, string password) =>
            api.SendAsync(HttpMethod.Put, Path, token, new { email, phone, currentPassword = password });
        await AccountTestClient.AssertProblemAsync(await Put(null, "valid@example.test", "0901234567", SeedPassword),
            HttpStatusCode.Unauthorized, ApiErrorCodes.AuthenticationRequired);
        await AccountTestClient.AssertProblemAsync(await Put(student, "valid@example.test", "0901234567", "wrong"),
            HttpStatusCode.BadRequest, ApiErrorCodes.InvalidCredentials);
        await AccountTestClient.AssertProblemAsync(await Put(student, duplicate, "0901234567", SeedPassword),
            HttpStatusCode.Conflict, ApiErrorCodes.ResourceConflict);
        await AccountTestClient.AssertProblemAsync(await Put(student, "valid@example.test", "123", SeedPassword),
            HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);

        var staff = await api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync(Path, staff)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherSessionsAndReturnsAnAuthenticatedReplacement()
    {
        using var app = new CustomWebApplicationFactory();
        var api = new AccountTestClient(app.CreateClient());
        var first = await api.LoginAsync("SV210002", SeedPassword);
        var other = await api.LoginAsync("SV210002", SeedPassword);
        var changed = await api.SendAsync(HttpMethod.Post, ChangePath, first,
            new { currentPassword = SeedPassword, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var data = (await AccountTestClient.ReadJsonAsync(changed)).GetProperty("data");
        var token = data.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync(Path, other)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync(Path, first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync(Path, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.LoginResponseAsync("SV210002", SeedPassword)).StatusCode);
        Assert.NotEmpty(await api.LoginAsync("SV210002", NewPassword));
    }

    [Fact]
    public async Task ChangePassword_EnforcesLengthLettersDigitsAccountCodeAndDifference()
    {
        using var app = new CustomWebApplicationFactory();
        var api = new AccountTestClient(app.CreateClient());
        var token = await api.LoginAsync("SV210002", SeedPassword);
        Task<HttpResponseMessage> Change(string jwt, string current, string next) =>
            api.SendAsync(HttpMethod.Post, ChangePath, jwt, new { currentPassword = current, newPassword = next });

        foreach (var (password, detail) in new[]
        {
            ("Short1", "8 đến 64"), (new string('a', 64) + "1", "8 đến 64"),
            ("alphabet", "chữ số"), ("12345678", "chữ cái"), ("sv210002Pw", "mã tài khoản")
        })
        {
            var result = await Change(token, SeedPassword, password);
            await AccountTestClient.AssertProblemAsync(result, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
            Assert.Contains(detail, (await AccountTestClient.ReadJsonAsync(result)).GetProperty("detail").GetString());
        }

        var changed = await Change(token, SeedPassword, NewPassword);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var newToken = (await AccountTestClient.ReadJsonAsync(changed)).GetProperty("data").GetProperty("token").GetString()!;
        var reused = await Change(newToken, NewPassword, NewPassword);
        await AccountTestClient.AssertProblemAsync(reused, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("khác mật khẩu", (await AccountTestClient.ReadJsonAsync(reused)).GetProperty("detail").GetString());
    }
}
