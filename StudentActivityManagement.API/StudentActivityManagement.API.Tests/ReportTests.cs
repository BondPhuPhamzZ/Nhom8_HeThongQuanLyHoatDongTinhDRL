using System.Globalization;
using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// US-24: period reports. All figures are checked in one scenario so other tests cannot change the counts;
/// the period spans only today ± 1 day, which keeps the seeded activities (ending days later) out of it.
/// </summary>
public sealed class ReportTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "123456";
    private readonly AccountTestClient _api;

    public ReportTests(CustomWebApplicationFactory factory)
    {
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task Reports_CountRegistrationsAttendancePointsAndAtRiskStudents()
    {
        var staff = await StaffAsync();
        var classId = await ClassIdAsync(staff, "21DTH1");
        var periodStart = Iso(DateTime.UtcNow.AddDays(-1));
        var periodEnd = Iso(DateTime.UtcNow.AddDays(1));
        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/evaluation-periods", staff,
            new { name = "Kỳ báo cáo", startDate = periodStart, endDate = periodEnd, targetPoints = 10 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var periodId = (await DataAsync(created)).GetProperty("id").GetInt32();

        // Activity worth 7 points: SV210002 and SV210001 attend, a third classmate only registers; only SV210002 is scored.
        var third = await CreateStudentAsync(staff, classId);
        var activityId = await CreateActivityAsync(staff, 7);
        var registrations = new Dictionary<string, int>();
        foreach (var (code, token) in new[] { ("SV210002", await _api.LoginAsync("SV210002", Password)), ("SV210001", await _api.LoginAsync("SV210001", Password)), (third.Code, third.Token) })
        {
            Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", token, null)).StatusCode);
        }
        var participants = (await DataAsync(await _api.GetAsync($"/api/v1/admin/activities/{activityId}/participants", staff)))
            .GetProperty("participants").GetProperty("items");
        foreach (var item in participants.EnumerateArray())
        {
            registrations[item.GetProperty("studentCode").GetString()!] = item.GetProperty("registrationId").GetInt32();
        }
        var sessionId = (await DataAsync(await _api.GetAsync($"/api/v1/admin/activities/{activityId}", staff))).GetProperty("sessions")[0].GetProperty("id").GetInt32();
        foreach (var code in new[] { "SV210002", "SV210001" })
        {
            Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
                $"/api/v1/admin/activities/{activityId}/sessions/{sessionId}/check-ins", staff, new { registrationId = registrations[code] })).StatusCode);
        }
        foreach (var status in new[] { "Ongoing", "Completed" })
        {
            Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/activities/{activityId}/status", staff, new { status })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/activities/{activityId}/scoring/approve", staff,
            new { registrationIds = new[] { registrations["SV210002"] } })).StatusCode);

        // A cancelled activity in the same period is reported separately, not counted.
        var cancelledId = await CreateActivityAsync(staff, 3);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/activities/{cancelledId}/status", staff, new { status = "Cancelled" })).StatusCode);

        var filter = $"periodId={periodId}&classId={classId}";
        var overview = await DataAsync(await _api.GetAsync($"/api/v1/admin/reports/overview?{filter}", staff));
        Assert.Equal(1, overview.GetProperty("activityCount").GetInt32());
        Assert.Equal(1, overview.GetProperty("cancelledActivityCount").GetInt32());
        Assert.Equal(3, overview.GetProperty("studentCount").GetInt32());
        Assert.Equal(3, overview.GetProperty("registrationCount").GetInt32());
        Assert.Equal(2, overview.GetProperty("attendedCount").GetInt32());
        Assert.Equal(66.7, overview.GetProperty("participationRate").GetDouble());
        Assert.Equal(7, overview.GetProperty("approvedPoints").GetInt32());
        Assert.Equal(2.3, overview.GetProperty("averagePoints").GetDouble());
        Assert.Equal(3, overview.GetProperty("atRiskCount").GetInt32());
        var classRow = Assert.Single(overview.GetProperty("groups").EnumerateArray());
        Assert.Equal("21DTH1", classRow.GetProperty("label").GetString());

        var activities = await DataAsync(await _api.GetAsync($"/api/v1/admin/reports/activities?{filter}", staff));
        var row = Assert.Single(activities.EnumerateArray());
        Assert.Equal(activityId, row.GetProperty("activityId").GetInt32());
        Assert.Equal(3, row.GetProperty("registrationCount").GetInt32());
        Assert.Equal(1, row.GetProperty("approvedCount").GetInt32());
        Assert.Equal(7, row.GetProperty("approvedPoints").GetInt32());

        var atRisk = await DataAsync(await _api.GetAsync($"/api/v1/admin/reports/at-risk-students?{filter}", staff));
        var items = atRisk.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, atRisk.GetProperty("totalCount").GetInt32());
        Assert.Equal("SV210002", items[^1].GetProperty("studentCode").GetString());
        Assert.Equal(3, items[^1].GetProperty("missingPoints").GetInt32());

        // Lowering the target to 5 leaves SV210002 (7 points) out of the at-risk list.
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/evaluation-periods/{periodId}", staff,
            new { name = "Kỳ báo cáo", startDate = periodStart, endDate = periodEnd, targetPoints = 5 })).StatusCode);
        var lowered = await DataAsync(await _api.GetAsync($"/api/v1/admin/reports/overview?{filter}", staff));
        Assert.Equal(2, lowered.GetProperty("atRiskCount").GetInt32());
        Assert.Equal(1, lowered.GetProperty("studentsMeetingTarget").GetInt32());

        // Without periodId the open period containing today is used.
        var current = await DataAsync(await _api.GetAsync($"/api/v1/admin/reports/overview?classId={classId}&groupBy=academicYear", staff));
        Assert.Equal(periodId, current.GetProperty("period").GetProperty("id").GetInt32());
        Assert.Equal("academicYear", current.GetProperty("groupBy").GetString());
    }

    [Fact]
    public async Task Reports_ValidateInputAndRequireBusinessAdmin()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", Password);

        Assert.Equal(HttpStatusCode.BadRequest, (await _api.GetAsync("/api/v1/admin/reports/overview?groupBy=gender", staff)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync("/api/v1/admin/reports/overview?periodId=999999", staff)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync("/api/v1/admin/reports/overview", student)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync("/api/v1/admin/reports/at-risk-students", null)).StatusCode);
    }

    private Task<string> StaffAsync() => _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    private async Task<int> ClassIdAsync(string staff, string classCode) =>
        (await DataAsync(await _api.GetAsync("/api/v1/admin/classes", staff))).EnumerateArray()
            .Single(item => item.GetProperty("classCode").GetString() == classCode).GetProperty("id").GetInt32();

    private async Task<(string Code, string Token)> CreateStudentAsync(string staff, int classId)
    {
        var code = $"SV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/students", staff, new
        {
            studentCode = code,
            fullName = "Sinh viên báo cáo",
            email = $"{code.ToLowerInvariant()}@student.edu.vn",
            password = Password,
            campus = "Cơ sở 1",
            academicYear = "K21",
            classId
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (code, await _api.LoginAsync(code, Password));
    }

    /// <summary>Published activity whose only session starts in 5 minutes and ends within the hour (inside the period).</summary>
    private async Task<int> CreateActivityAsync(string staff, int trainingPoints)
    {
        var start = DateTime.UtcNow.AddMinutes(5);
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"Report-{Guid.NewGuid():N}",
            description = "Kiểm thử báo cáo",
            organizerName = "Phòng CTSV",
            registrationOpenTime = Iso(DateTime.UtcNow.AddHours(-1)),
            registrationCloseTime = Iso(DateTime.UtcNow.AddMinutes(2)),
            maxParticipants = 10,
            trainingPoints,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường", startTime = Iso(start), endTime = Iso(start.AddMinutes(50)), sortOrder = 1 }
            },
            audienceRules = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
