using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Tests;

/// <summary>US-07: session check-in codes and code-based student check-in.</summary>
public sealed class CheckInTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string StudentCode = "SV210002";
    private const string MonitorCode = "SV210001";
    private const string StudentPassword = "123456";
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public CheckInTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task CheckIn_WithIssuedCode_MarksAttendedAndIsCounted()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);

        var issued = await _api.SendAsync(HttpMethod.Post, CodeUrl(activityId, sessionId), staff, null);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var code = (await DataAsync(issued)).GetProperty("code").GetString()!;
        Assert.Matches("^[A-HJ-NP-Z2-9]{6}$", code);

        var before = await DataAsync(await _api.GetAsync(StatusUrl(activityId), student));
        Assert.Equal("Code", before.GetProperty("method").GetString());
        Assert.True(before.GetProperty("sessions")[0].GetProperty("canCheckIn").GetBoolean());

        var checkIn = await CheckInAsync(student, activityId, sessionId, $" {code.ToLowerInvariant()} ");
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        Assert.Equal("Attended", (await DataAsync(checkIn)).GetProperty("registrationStatus").GetString());

        var after = await DataAsync(await _api.GetAsync(StatusUrl(activityId), student));
        Assert.Equal("Attended", after.GetProperty("registrationStatus").GetString());
        var session = after.GetProperty("sessions")[0];
        Assert.NotEqual(JsonValueKind.Null, session.GetProperty("checkedInAt").ValueKind);
        Assert.False(session.GetProperty("canCheckIn").GetBoolean());

        var codes = await DataAsync(await _api.GetAsync($"/api/v1/admin/activities/{activityId}/check-in-codes", staff));
        Assert.Equal(1, codes.GetProperty("sessions")[0].GetProperty("checkInCount").GetInt32());

        // Attendance is history: the registration can no longer be cancelled.
        var cancel = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/student-activities/{activityId}/registration", student, null);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
    }

    [Fact]
    public async Task CheckIn_WrongCode_Returns400_AndRotatedCodeReplacesOldOne()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);
        var oldCode = await IssueCodeAsync(staff, activityId, sessionId);
        var newCode = await IssueCodeAsync(staff, activityId, sessionId);
        Assert.NotEqual(oldCode, newCode);

        var wrong = await CheckInAsync(student, activityId, sessionId, oldCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("validation_failed", await ErrorCodeAsync(wrong));

        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(student, activityId, sessionId, newCode)).StatusCode);
    }

    [Fact]
    public async Task CheckIn_Twice_Returns409()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);
        var code = await IssueCodeAsync(staff, activityId, sessionId);
        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(student, activityId, sessionId, code)).StatusCode);

        var again = await CheckInAsync(student, activityId, sessionId, code);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("resource_conflict", await ErrorCodeAsync(again));
    }

    [Fact]
    public async Task CheckIn_ConcurrentRequests_RecordOnlyOneRow()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);
        var code = await IssueCodeAsync(staff, activityId, sessionId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CheckInAsync(student, activityId, sessionId, code)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.ActivityCheckIns.CountAsync(item => item.SessionId == sessionId));
    }

    [Fact]
    public async Task CheckIn_OutsideWindow_Returns409()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);

        // Session starts in 3 hours: the default window opens only 15 minutes before.
        var (laterActivity, laterSession) = await CreateRegisteredActivityAsync(staff, student, sessionStartsIn: TimeSpan.FromHours(3));
        var laterCode = await IssueCodeAsync(staff, laterActivity, laterSession);
        var early = await CheckInAsync(student, laterActivity, laterSession, laterCode);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(early));

        // Session ended 2 hours ago: the default window closed 30 minutes after the end.
        var (pastActivity, pastSession) = await CreateRegisteredActivityAsync(staff, student);
        var pastCode = await IssueCodeAsync(staff, pastActivity, pastSession);
        await MoveSessionAsync(pastSession, DateTime.UtcNow.AddHours(-4), DateTime.UtcNow.AddHours(-2));
        Assert.Equal(HttpStatusCode.Conflict, (await CheckInAsync(student, pastActivity, pastSession, pastCode)).StatusCode);
    }

    [Fact]
    public async Task CheckIn_BeforeCodeIssued_Returns409()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);

        var response = await CheckInAsync(student, activityId, sessionId, "ABCDEF");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ManualMethod_RejectsCodesAndStudentCheckIn()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);
        var code = await IssueCodeAsync(staff, activityId, sessionId);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.ActivityCheckInConfigs.Add(new ActivityCheckInConfig
            {
                ActivityId = activityId,
                Method = CheckInMethods.Manual,
                OpenBeforeMinutes = 15,
                CloseAfterMinutes = 30,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await CheckInAsync(student, activityId, sessionId, code)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Post, CodeUrl(activityId, sessionId), staff, null)).StatusCode);
        var status = await DataAsync(await _api.GetAsync(StatusUrl(activityId), student));
        Assert.Equal("Manual", status.GetProperty("method").GetString());
        Assert.False(status.GetProperty("sessions")[0].GetProperty("canCheckIn").GetBoolean());
    }

    [Fact]
    public async Task CheckIn_WithoutActiveRegistration_IsRejected()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var monitor = await _api.LoginAsync(MonitorCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);
        var code = await IssueCodeAsync(staff, activityId, sessionId);

        var notRegistered = await CheckInAsync(monitor, activityId, sessionId, code);
        Assert.Equal(HttpStatusCode.NotFound, notRegistered.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(StatusUrl(activityId), monitor)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/student-activities/{activityId}/registration", student, null)).StatusCode);
        var cancelled = await CheckInAsync(student, activityId, sessionId, code);
        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
    }

    [Fact]
    public async Task SessionOfAnotherActivity_Returns404()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, _) = await CreateRegisteredActivityAsync(staff, student);
        var (_, otherSession) = await CreateRegisteredActivityAsync(staff, student);

        Assert.Equal(HttpStatusCode.NotFound, (await CheckInAsync(student, activityId, otherSession, "ABCDEF")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Post, CodeUrl(activityId, otherSession), staff, null)).StatusCode);
    }

    [Fact]
    public async Task IssueCode_ForCompletedActivity_Returns409()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);
        foreach (var status in new[] { "Ongoing", "Completed" })
        {
            Assert.Equal(HttpStatusCode.OK,
                (await _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/activities/{activityId}/status", staff, new { status })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Post, CodeUrl(activityId, sessionId), staff, null)).StatusCode);
    }

    [Fact]
    public async Task Endpoints_EnforceRoles()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var (activityId, sessionId) = await CreateRegisteredActivityAsync(staff, student);

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync($"/api/v1/admin/activities/{activityId}/check-in-codes", student)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Post, CodeUrl(activityId, sessionId), student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CheckInAsync(staff, activityId, sessionId, "ABCDEF")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CheckInAsync(null, activityId, sessionId, "ABCDEF")).StatusCode);
    }

    private Task<string> StaffAsync() => _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    /// <summary>Creates a published one-session activity and registers the student; by default the session starts in 5 minutes.</summary>
    private async Task<(int ActivityId, int SessionId)> CreateRegisteredActivityAsync(string staff, string student, TimeSpan? sessionStartsIn = null)
    {
        var start = DateTime.UtcNow.Add(sessionStartsIn ?? TimeSpan.FromMinutes(5));
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"CheckIn-{Guid.NewGuid():N}",
            description = "Kiểm thử check-in",
            organizerName = "Phòng CTSV",
            registrationOpenTime = Iso(DateTime.UtcNow.AddHours(-1)),
            registrationCloseTime = Iso(DateTime.UtcNow.AddMinutes(2)),
            maxParticipants = 10,
            trainingPoints = 5,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường", startTime = Iso(start), endTime = Iso(start.AddHours(2)), sortOrder = 1 }
            },
            audienceRules = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activity = await DataAsync(response);
        var activityId = activity.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", student, null)).StatusCode);
        return (activityId, activity.GetProperty("sessions")[0].GetProperty("id").GetInt32());
    }

    private async Task<string> IssueCodeAsync(string staff, int activityId, int sessionId)
    {
        var response = await _api.SendAsync(HttpMethod.Post, CodeUrl(activityId, sessionId), staff, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await DataAsync(response)).GetProperty("code").GetString()!;
    }

    private async Task MoveSessionAsync(int sessionId, DateTime startUtc, DateTime endUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await context.ActivitySessions.SingleAsync(item => item.Id == sessionId);
        session.StartTime = startUtc;
        session.EndTime = endUtc;
        await context.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> CheckInAsync(string? token, int activityId, int sessionId, string code) =>
        _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/sessions/{sessionId}/check-in", token, new { code });

    private static string CodeUrl(int activityId, int sessionId) => $"/api/v1/admin/activities/{activityId}/sessions/{sessionId}/check-in-code";

    private static string StatusUrl(int activityId) => $"/api/v1/student-activities/{activityId}/check-in-status";

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errorCode").GetString();
    }
}
