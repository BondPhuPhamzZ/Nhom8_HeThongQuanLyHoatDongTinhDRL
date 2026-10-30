using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// Students keep access to activities they registered for after the activity ends, so they can submit
/// evidence (US-08) within the submission window; others still only see upcoming eligible activities.
/// </summary>
public sealed class RegisteredActivityAccessTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string StudentCode = "SV210002";
    private const string MonitorCode = "SV210001";
    private const string Password = "123456";
    private readonly AccountTestClient _api;
    private readonly HttpClient _client;

    public RegisteredActivityAccessTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _api = new AccountTestClient(_client);
    }

    [Fact]
    public async Task CompletedActivity_StaysOpenToRegisteredStudent_AndAcceptsEvidence()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync(StudentCode, Password);
        var monitor = await _api.LoginAsync(MonitorCode, Password);
        var id = await CreateCompletedActivityAsync(staff, student);

        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync($"/api/v1/student-activities/{id}", student)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/api/v1/student-activities/{id}", monitor)).StatusCode);

        var mine = await DataAsync(await _api.GetAsync("/api/v1/student-activities?registered=true&pageSize=100", student));
        Assert.Contains(mine.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);
        var upcoming = await DataAsync(await _api.GetAsync("/api/v1/student-activities?pageSize=100", student));
        Assert.DoesNotContain(upcoming.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student-activities/{id}/evidences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", student);
        request.Content = new MultipartFormDataContent
        {
            { new StringContent("ConfirmationCode"), "evidenceType" },
            { new StringContent("BTC-2026-01"), "confirmationCode" }
        };
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task CancelledRegistration_DoesNotGrantAccessToEndedActivity()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync(StudentCode, Password);
        var id = await CreateCompletedActivityAsync(staff, student, cancelBeforeEnd: true);

        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/api/v1/student-activities/{id}", student)).StatusCode);
        var mine = await DataAsync(await _api.GetAsync("/api/v1/student-activities?registered=true&pageSize=100", student));
        Assert.DoesNotContain(mine.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);
    }

    /// <summary>Published activity, student registers (and optionally cancels), then Ongoing → Completed.</summary>
    private async Task<int> CreateCompletedActivityAsync(string staff, string student, bool cancelBeforeEnd = false)
    {
        var start = DateTime.UtcNow.AddMinutes(5);
        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"Access-{Guid.NewGuid():N}",
            description = "Kiểm thử truy cập hoạt động đã đăng ký",
            organizerName = "Phòng CTSV",
            registrationOpenTime = Iso(DateTime.UtcNow.AddHours(-1)),
            registrationCloseTime = Iso(DateTime.UtcNow.AddMinutes(2)),
            maxParticipants = 10,
            trainingPoints = 5,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường", startTime = Iso(start), endTime = Iso(start.AddHours(1)), sortOrder = 1 }
            },
            audienceRules = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await DataAsync(created)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{id}/register", student, null)).StatusCode);
        if (cancelBeforeEnd)
        {
            Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/student-activities/{id}/registration", student, null)).StatusCode);
        }

        foreach (var status in new[] { "Ongoing", "Completed" })
        {
            Assert.Equal(HttpStatusCode.OK,
                (await _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/activities/{id}/status", staff, new { status })).StatusCode);
        }

        return id;
    }

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
