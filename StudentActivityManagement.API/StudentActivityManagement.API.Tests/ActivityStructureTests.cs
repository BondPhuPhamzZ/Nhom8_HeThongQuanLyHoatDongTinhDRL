using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

public sealed class ActivityStructureTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ActivityStructureTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SeededActivities_ExposeOrganizerSessionsAndEligibility()
    {
        await AuthorizeAsync("SV210002", "123456");
        var response = await _client.GetAsync("/api/v1/student-activities");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var activities = document.RootElement.GetProperty("data").GetProperty("items");

        Assert.NotEmpty(activities.EnumerateArray());
        Assert.All(activities.EnumerateArray(), activity =>
        {
            Assert.False(string.IsNullOrWhiteSpace(activity.GetProperty("organizerName").GetString()));
            Assert.NotEmpty(activity.GetProperty("sessions").EnumerateArray());
            Assert.True(activity.GetProperty("eligibility").GetProperty("isEligible").GetBoolean());
        });
    }

    [Fact]
    public async Task AudienceRules_FilterAndProtectListDetailAndRegistration()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityName = $"Audience-{Guid.NewGuid():N}";
        var activityId = await CreateActivityAsync(activityName, "K22", twoSessions: true);

        await AuthorizeAsync("SV210002", "123456");
        var hiddenList = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/student-activities?search={Uri.EscapeDataString(activityName)}");
        Assert.Equal(0, hiddenList.GetProperty("data").GetProperty("totalCount").GetInt32());

        var hiddenDetail = await _client.GetAsync($"/api/v1/student-activities/{activityId}");
        var blockedRegistration = await _client.PostAsync(
            $"/api/v1/student-activities/{activityId}/register",
            null);
        Assert.Equal(HttpStatusCode.NotFound, hiddenDetail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, blockedRegistration.StatusCode);

        // Sinh viên do Phòng QL HSSV tạo (US-25 đã gỡ tự đăng ký công khai).
        var suffix = Guid.NewGuid().ToString("N")[..10];
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var registration = await _client.PostAsJsonAsync("/api/v1/admin/students", new
        {
            studentCode = $"SV22{suffix}",
            fullName = "Sinh viên K22 kiểm thử",
            email = $"sv22{suffix}@student.edu.vn",
            password = "123456",
            phone = "0900000000",
            campus = "Cơ sở 1",
            academicYear = "K22",
            classId = 2
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        await AuthorizeAsync($"SV22{suffix}", "123456");

        var visibleList = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/student-activities?search={Uri.EscapeDataString(activityName)}");
        var visibleActivity = visibleList.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(1, visibleList.GetProperty("data").GetProperty("totalCount").GetInt32());
        Assert.Equal(2, visibleActivity.GetProperty("sessions").GetArrayLength());
        Assert.Equal(
            "Required",
            visibleActivity.GetProperty("eligibility").GetProperty("participationType").GetString());

        var allowedRegistration = await _client.PostAsync(
            $"/api/v1/student-activities/{activityId}/register",
            null);
        Assert.Equal(HttpStatusCode.OK, allowedRegistration.StatusCode);
    }

    [Fact]
    public async Task UpdateActivity_ReplacesSessionsWithoutUniqueOrderConflicts()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityName = $"Update-{Guid.NewGuid():N}";
        var activityId = await CreateActivityAsync(activityName, academicYear: null, twoSessions: true);
        var now = DateTime.UtcNow;

        var response = await _client.PutAsJsonAsync($"/api/v1/admin/activities/{activityId}", new
        {
            activityName,
            description = "Cập nhật lịch",
            organizerName = "Đơn vị mới",
            registrationOpenTime = now.AddHours(-1),
            registrationCloseTime = now.AddHours(6),
            maxParticipants = 20,
            trainingPoints = 8,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new
                {
                    sessionName = "Buổi thay thế",
                    campus = "Cơ sở 1",
                    location = "Phòng C",
                    startTime = now.AddDays(1),
                    endTime = now.AddDays(1).AddHours(3),
                    sortOrder = 1
                }
            },
            audienceRules = Array.Empty<object>()
        });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var activity = document.RootElement.GetProperty("data");
        Assert.Equal("Đơn vị mới", activity.GetProperty("organizerName").GetString());
        Assert.Equal(1, activity.GetProperty("sessions").GetArrayLength());
        Assert.Equal("Buổi thay thế", activity.GetProperty("sessions")[0].GetProperty("sessionName").GetString());
    }

    private async Task<int> CreateActivityAsync(string name, string? academicYear, bool twoSessions)
    {
        var now = DateTime.UtcNow;
        var sessions = new List<object>
        {
            new
            {
                sessionName = "Buổi 1",
                campus = "Cơ sở 1",
                location = "Phòng A",
                startTime = now.AddDays(1),
                endTime = now.AddDays(1).AddHours(2),
                sortOrder = 1
            }
        };
        if (twoSessions)
        {
            sessions.Add(new
            {
                sessionName = "Buổi 2",
                campus = "Cơ sở 2",
                location = "Phòng B",
                startTime = now.AddDays(2),
                endTime = now.AddDays(2).AddHours(2),
                sortOrder = 2
            });
        }

        var rules = academicYear == null
            ? Array.Empty<object>()
            : new object[] { new { academicYear, participationType = "Required" } };
        var response = await _client.PostAsJsonAsync("/api/v1/admin/activities", new
        {
            activityName = name,
            description = "Kiểm thử cấu trúc hoạt động",
            organizerName = "Phòng CTSV",
            registrationOpenTime = now.AddHours(-1),
            registrationCloseTime = now.AddHours(6),
            maxParticipants = 10,
            trainingPoints = 5,
            targetAudience = academicYear == null ? "Tất cả sinh viên" : $"Sinh viên {academicYear}",
            status = "Published",
            sessions,
            audienceRules = rules
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task AuthorizeAsync(string usernameOrEmail, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            usernameOrEmail,
            password
        });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("data").GetProperty("token").GetString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
