using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>US-22: confirm results of a completed activity and record points once per registration.</summary>
public sealed class ScoringTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "123456";
    private readonly AccountTestClient _api;
    private readonly HttpClient _client;

    public ScoringTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _api = new AccountTestClient(_client);
    }

    [Fact]
    public async Task Scoring_IsBlockedUntilActivityCompleted()
    {
        var setup = await SetupAsync(complete: false);

        var sheet = await DataAsync(await _api.GetAsync(ScoringUrl(setup.ActivityId), setup.Staff));
        Assert.False(sheet.GetProperty("canApprove").GetBoolean());
        Assert.False(string.IsNullOrEmpty(sheet.GetProperty("blockedReason").GetString()));

        var approve = await ApproveAsync(setup, null);
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    [Fact]
    public async Task ApproveAll_RecordsAttendedAndAcceptedEvidence_OnceWithHistory()
    {
        var setup = await SetupAsync(complete: true);

        var sheet = await DataAsync(await _api.GetAsync(ScoringUrl(setup.ActivityId), setup.Staff));
        Assert.True(sheet.GetProperty("canApprove").GetBoolean());
        Assert.Equal(2, sheet.GetProperty("eligibleCount").GetInt32());
        var absent = Candidate(sheet, setup.AbsentRegistrationId);
        Assert.False(absent.GetProperty("eligible").GetBoolean());

        var approved = await ApproveAsync(setup, null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(2, (await DataAsync(approved)).GetProperty("approvedCount").GetInt32());

        var after = await DataAsync(await _api.GetAsync(ScoringUrl(setup.ActivityId), setup.Staff));
        Assert.Equal(2, after.GetProperty("approvedCount").GetInt32());
        var record = Candidate(after, setup.AttendedRegistrationId).GetProperty("pointRecord");
        Assert.Equal("Approved", record.GetProperty("status").GetString());
        Assert.Equal(7, record.GetProperty("points").GetInt32());
        Assert.Equal("Approved", Candidate(after, setup.EvidenceRegistrationId).GetProperty("pointRecord").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, Candidate(after, setup.AbsentRegistrationId).GetProperty("pointRecord").ValueKind);

        // Approving the same registrations again records nothing new.
        var again = await DataAsync(await ApproveAsync(setup, [setup.AttendedRegistrationId, setup.AbsentRegistrationId]));
        Assert.Equal(0, again.GetProperty("approvedCount").GetInt32());
        Assert.Equal(2, again.GetProperty("skipped").GetArrayLength());

        var history = await DataAsync(await _api.GetAsync($"/api/v1/admin/point-records/{record.GetProperty("id").GetInt32()}/history", setup.Staff));
        var entry = Assert.Single(history.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("fromStatus").ValueKind);
        Assert.Equal("Approved", entry.GetProperty("toStatus").GetString());
        Assert.False(string.IsNullOrEmpty(entry.GetProperty("changedByName").GetString()));
    }

    [Fact]
    public async Task ScoredRegistration_FreezesAttendanceAndEvidenceReview()
    {
        var setup = await SetupAsync(complete: true);
        Assert.Equal(HttpStatusCode.OK, (await ApproveAsync(setup, [setup.AttendedRegistrationId])).StatusCode);

        var undo = await _api.SendAsync(HttpMethod.Delete,
            $"/api/v1/admin/activities/{setup.ActivityId}/sessions/{setup.SessionId}/check-ins/{setup.AttendedRegistrationId}", setup.Staff, null);
        Assert.Equal(HttpStatusCode.Conflict, undo.StatusCode);

        // The other registration is not scored yet, so its evidence can still be reviewed.
        Assert.Equal(HttpStatusCode.OK, (await ReviewAsync(setup, setup.EvidenceId, "Pending")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ReviewAsync(setup, setup.EvidenceId, "Accepted")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ApproveAsync(setup, [setup.EvidenceRegistrationId])).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ReviewAsync(setup, setup.EvidenceId, "Rejected", "Thử đổi sau khi ghi điểm")).StatusCode);
    }

    [Fact]
    public async Task ClosedPeriod_AndActivityOutsideAnyPeriod_BlockApproval()
    {
        var setup = await SetupAsync(complete: true);
        var periodId = (await DataAsync(await _api.GetAsync(ScoringUrl(setup.ActivityId), setup.Staff)))
            .GetProperty("period").GetProperty("id").GetInt32();

        await SetPeriodStatusAsync(setup.Staff, periodId, "Closed");
        try
        {
            Assert.Equal(HttpStatusCode.Conflict, (await ApproveAsync(setup, null)).StatusCode);
        }
        finally
        {
            await SetPeriodStatusAsync(setup.Staff, periodId, "Open");
        }

        var farFuture = await SetupAsync(complete: true, sessionStart: new DateTime(2045, 3, 1, 1, 0, 0, DateTimeKind.Utc));
        var sheet = await DataAsync(await _api.GetAsync(ScoringUrl(farFuture.ActivityId), farFuture.Staff));
        Assert.Equal(JsonValueKind.Null, sheet.GetProperty("period").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await ApproveAsync(farFuture, null)).StatusCode);
    }

    [Fact]
    public async Task Scoring_RequiresBusinessAdmin()
    {
        var setup = await SetupAsync(complete: false);

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync(ScoringUrl(setup.ActivityId), setup.StudentToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _api.SendAsync(HttpMethod.Post, $"{ScoringUrl(setup.ActivityId)}/approve", setup.StudentToken, new { })).StatusCode);
    }

    private sealed record Setup(
        string Staff, string StudentToken, int ActivityId, int SessionId,
        int AttendedRegistrationId, int EvidenceRegistrationId, int AbsentRegistrationId, int EvidenceId);

    /// <summary>
    /// Activity worth 7 points: SV210002 attends (manual check-in), SV210001 only submits a confirmation code that
    /// is accepted, and a third student registers but never attends. Optionally moved to Completed.
    /// </summary>
    private async Task<Setup> SetupAsync(bool complete, DateTime? sessionStart = null)
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        await EnsureCurrentPeriodAsync(staff);
        var attended = await _api.LoginAsync("SV210002", Password);
        var withEvidence = await _api.LoginAsync("SV210001", Password);
        var absent = await CreateStudentAsync(staff);

        var start = sessionStart ?? DateTime.UtcNow.AddMinutes(5);
        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"Scoring-{Guid.NewGuid():N}",
            description = "Kiểm thử ghi điểm",
            organizerName = "Phòng CTSV",
            registrationOpenTime = Iso(DateTime.UtcNow.AddHours(-1)),
            registrationCloseTime = Iso(DateTime.UtcNow.AddMinutes(2)),
            maxParticipants = 10,
            trainingPoints = 7,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường", startTime = Iso(start), endTime = Iso(start.AddHours(1)), sortOrder = 1 }
            },
            audienceRules = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var activity = await DataAsync(created);
        var activityId = activity.GetProperty("id").GetInt32();
        var sessionId = activity.GetProperty("sessions")[0].GetProperty("id").GetInt32();
        foreach (var token in new[] { attended, withEvidence, absent })
        {
            Assert.Equal(HttpStatusCode.OK,
                (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", token, null)).StatusCode);
        }

        var participants = (await DataAsync(await _api.GetAsync($"/api/v1/admin/activities/{activityId}/participants", staff)))
            .GetProperty("participants").GetProperty("items").EnumerateArray().ToList();
        int RegistrationOf(string code) => participants.Single(item => item.GetProperty("studentCode").GetString() == code)
            .GetProperty("registrationId").GetInt32();
        var attendedId = RegistrationOf("SV210002");
        var evidenceRegistrationId = RegistrationOf("SV210001");
        var absentId = participants.Single(item => item.GetProperty("registrationId").GetInt32() != attendedId &&
                                                   item.GetProperty("registrationId").GetInt32() != evidenceRegistrationId)
            .GetProperty("registrationId").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/activities/{activityId}/sessions/{sessionId}/check-ins", staff, new { registrationId = attendedId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
            $"/api/v1/admin/activities/{activityId}/status", staff, new { status = "Ongoing" })).StatusCode);

        var evidenceId = await UploadCodeEvidenceAsync(withEvidence, activityId);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
            $"/api/v1/admin/activities/{activityId}/evidences/{evidenceId}/review", staff, new { status = "Accepted" })).StatusCode);

        if (complete)
        {
            Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
                $"/api/v1/admin/activities/{activityId}/status", staff, new { status = "Completed" })).StatusCode);
        }

        return new Setup(staff, attended, activityId, sessionId, attendedId, evidenceRegistrationId, absentId, evidenceId);
    }

    /// <summary>One shared period around today for the whole test class (periods may not overlap).</summary>
    private async Task EnsureCurrentPeriodAsync(string staff)
    {
        var periods = await DataAsync(await _api.GetAsync("/api/v1/admin/evaluation-periods", staff));
        if (periods.EnumerateArray().Any(item => item.GetProperty("name").GetString() == "Kỳ kiểm thử hiện tại"))
        {
            return;
        }

        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/evaluation-periods", staff, new
        {
            name = "Kỳ kiểm thử hiện tại",
            startDate = Iso(DateTime.UtcNow.AddDays(-60)),
            endDate = Iso(DateTime.UtcNow.AddDays(60)),
            targetPoints = 50
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private async Task<string> CreateStudentAsync(string staff)
    {
        var code = $"SV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/students", staff, new
        {
            studentCode = code,
            fullName = "Sinh viên vắng mặt",
            email = $"{code.ToLowerInvariant()}@student.edu.vn",
            password = Password,
            campus = "Cơ sở 1",
            academicYear = "K21"
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await _api.LoginAsync(code, Password);
    }

    private async Task<int> UploadCodeEvidenceAsync(string token, int activityId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/evidences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new MultipartFormDataContent
        {
            { new StringContent("ConfirmationCode"), "evidenceType" },
            { new StringContent("BTC-SCORING"), "confirmationCode" }
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> ApproveAsync(Setup setup, int[]? registrationIds) =>
        _api.SendAsync(HttpMethod.Post, $"{ScoringUrl(setup.ActivityId)}/approve", setup.Staff, new { registrationIds });

    private Task<HttpResponseMessage> ReviewAsync(Setup setup, int evidenceId, string status, string? note = null) =>
        _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/activities/{setup.ActivityId}/evidences/{evidenceId}/review", setup.Staff, new { status, note });

    private async Task SetPeriodStatusAsync(string staff, int periodId, string status) =>
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/evaluation-periods/{periodId}/status", staff, new { status })).StatusCode);

    private static JsonElement Candidate(JsonElement sheet, int registrationId) => sheet.GetProperty("candidates").EnumerateArray()
        .Single(item => item.GetProperty("registrationId").GetInt32() == registrationId);

    private static string ScoringUrl(int activityId) => $"/api/v1/admin/activities/{activityId}/scoring";

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
