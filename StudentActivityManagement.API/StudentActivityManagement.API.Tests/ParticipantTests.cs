using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Storage;

namespace StudentActivityManagement.API.Tests;

/// <summary>US-21: participant list, manual attendance and evidence review for Phòng QL HSSV.</summary>
public sealed class ParticipantTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string StudentCode = "SV210002";
    private const string MonitorCode = "SV210001";
    private const string StudentPassword = "123456";
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 7, 7, 7];
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public ParticipantTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task Participants_ListsRegistrationsWithSummary_AndFilters()
    {
        var setup = await SetupAsync();

        var all = await DataAsync(await _api.GetAsync(ParticipantsUrl(setup.ActivityId), setup.Staff));
        Assert.Equal(2, all.GetProperty("summary").GetProperty("active").GetInt32());
        Assert.Equal(2, all.GetProperty("summary").GetProperty("registered").GetInt32());
        var items = all.GetProperty("participants").GetProperty("items");
        Assert.Equal(new[] { MonitorCode, StudentCode }, items.EnumerateArray().Select(item => item.GetProperty("studentCode").GetString()));
        Assert.Equal(1, all.GetProperty("sessions").GetArrayLength());

        var searched = await DataAsync(await _api.GetAsync($"{ParticipantsUrl(setup.ActivityId)}?search={StudentCode.ToLowerInvariant()}", setup.Staff));
        Assert.Equal(1, searched.GetProperty("participants").GetProperty("totalCount").GetInt32());

        var attended = await DataAsync(await _api.GetAsync($"{ParticipantsUrl(setup.ActivityId)}?status=attended", setup.Staff));
        Assert.Equal(0, attended.GetProperty("participants").GetProperty("totalCount").GetInt32());

        var invalid = await _api.GetAsync($"{ParticipantsUrl(setup.ActivityId)}?status=Unknown", setup.Staff);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(ParticipantsUrl(999999), setup.Staff)).StatusCode);
    }

    [Fact]
    public async Task ManualCheckIn_MarksAttended_RecordsStaff_AndCanBeUndone()
    {
        var setup = await SetupAsync();

        var recorded = await RecordAsync(setup, setup.StudentRegistrationId);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var detail = await DataAsync(recorded);
        Assert.Equal("Attended", detail.GetProperty("registrationStatus").GetString());
        var checkIn = detail.GetProperty("checkIns")[0];
        Assert.Equal("Manual", checkIn.GetProperty("method").GetString());
        Assert.False(string.IsNullOrEmpty(checkIn.GetProperty("recordedByName").GetString()));

        var again = await RecordAsync(setup, setup.StudentRegistrationId);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("resource_conflict", await ErrorCodeAsync(again));

        var list = await DataAsync(await _api.GetAsync($"{ParticipantsUrl(setup.ActivityId)}?status=Attended", setup.Staff));
        Assert.Equal(1, list.GetProperty("participants").GetProperty("totalCount").GetInt32());
        Assert.Equal(1, list.GetProperty("sessions")[0].GetProperty("checkInCount").GetInt32());

        var undone = await _api.SendAsync(HttpMethod.Delete, UndoUrl(setup, setup.StudentRegistrationId), setup.Staff, null);
        Assert.Equal(HttpStatusCode.OK, undone.StatusCode);
        var afterUndo = await DataAsync(undone);
        Assert.Equal("Registered", afterUndo.GetProperty("registrationStatus").GetString());
        Assert.Equal(0, afterUndo.GetProperty("checkIns").GetArrayLength());

        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Delete, UndoUrl(setup, setup.StudentRegistrationId), setup.Staff, null)).StatusCode);
    }

    [Fact]
    public async Task ManualCheckIn_RejectsCancelledOrForeignRegistrationsAndSessions()
    {
        var setup = await SetupAsync();
        var other = await SetupAsync();

        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/student-activities/{setup.ActivityId}/registration", setup.Student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await RecordAsync(setup, setup.StudentRegistrationId)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await RecordAsync(setup, other.MonitorRegistrationId)).StatusCode);
        var foreignSession = await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/activities/{setup.ActivityId}/sessions/{other.SessionId}/check-ins", setup.Staff,
            new { registrationId = setup.MonitorRegistrationId });
        Assert.Equal(HttpStatusCode.NotFound, foreignSession.StatusCode);
    }

    [Fact]
    public async Task ManualCheckIn_ForCancelledActivity_Returns409()
    {
        var setup = await SetupAsync();
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
            $"/api/v1/admin/activities/{setup.ActivityId}/status", setup.Staff, new { status = "Cancelled" })).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await RecordAsync(setup, setup.MonitorRegistrationId)).StatusCode);
    }

    [Fact]
    public async Task CodeCheckIn_ShowsUpAsCodeMethod()
    {
        var setup = await SetupAsync();
        var issued = await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/activities/{setup.ActivityId}/sessions/{setup.SessionId}/check-in-code", setup.Staff, null);
        var code = (await DataAsync(issued)).GetProperty("code").GetString();
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/student-activities/{setup.ActivityId}/sessions/{setup.SessionId}/check-in", setup.Student, new { code })).StatusCode);

        var detail = await DataAsync(await _api.GetAsync($"{ParticipantsUrl(setup.ActivityId)}/{setup.StudentRegistrationId}", setup.Staff));
        var checkIn = detail.GetProperty("checkIns")[0];
        Assert.Equal("Code", checkIn.GetProperty("method").GetString());
        Assert.Equal(JsonValueKind.Null, checkIn.GetProperty("recordedByName").ValueKind);
    }

    [Fact]
    public async Task EvidenceReview_AcceptRejectReopen_AndDownload()
    {
        var setup = await SetupAsync();
        var fileEvidenceId = await AddEvidenceAsync(setup.StudentRegistrationId, withFile: true);
        var codeEvidenceId = await AddEvidenceAsync(setup.StudentRegistrationId, withFile: false);

        var pending = await DataAsync(await _api.GetAsync($"{ParticipantsUrl(setup.ActivityId)}?hasPendingEvidence=true", setup.Staff));
        Assert.Equal(2, pending.GetProperty("summary").GetProperty("pendingEvidence").GetInt32());
        Assert.Equal(1, pending.GetProperty("participants").GetProperty("totalCount").GetInt32());
        Assert.Equal(2, pending.GetProperty("participants").GetProperty("items")[0].GetProperty("pendingEvidenceCount").GetInt32());

        var rejectWithoutNote = await ReviewAsync(setup, fileEvidenceId, "Rejected", null);
        Assert.Equal(HttpStatusCode.BadRequest, rejectWithoutNote.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReviewAsync(setup, fileEvidenceId, "Approved", null)).StatusCode);

        var rejected = await ReviewAsync(setup, fileEvidenceId, "rejected", "Ảnh mờ, không thấy mặt sinh viên");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedData = await DataAsync(rejected);
        Assert.Equal("Rejected", rejectedData.GetProperty("reviewStatus").GetString());
        Assert.False(string.IsNullOrEmpty(rejectedData.GetProperty("reviewedByName").GetString()));

        var reopened = await DataAsync(await ReviewAsync(setup, fileEvidenceId, "Pending", null));
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("reviewedAt").ValueKind);
        Assert.Equal("Accepted", (await DataAsync(await ReviewAsync(setup, codeEvidenceId, "Accepted", null))).GetProperty("reviewStatus").GetString());

        var detail = await DataAsync(await _api.GetAsync($"{ParticipantsUrl(setup.ActivityId)}/{setup.StudentRegistrationId}", setup.Staff));
        var evidences = detail.GetProperty("evidences").EnumerateArray().ToList();
        Assert.Equal(2, evidences.Count);
        var fileEvidence = evidences.Single(item => item.GetProperty("id").GetInt32() == fileEvidenceId);
        Assert.Equal($"/api/v1/admin/activities/{setup.ActivityId}/evidences/{fileEvidenceId}/file", fileEvidence.GetProperty("fileUrl").GetString());
        Assert.Equal(JsonValueKind.Null, evidences.Single(item => item.GetProperty("id").GetInt32() == codeEvidenceId).GetProperty("fileUrl").ValueKind);

        var download = await _api.GetAsync($"/api/v1/admin/activities/{setup.ActivityId}/evidences/{fileEvidenceId}/file", setup.Staff);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngBytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound,
            (await _api.GetAsync($"/api/v1/admin/activities/{setup.ActivityId}/evidences/{codeEvidenceId}/file", setup.Staff)).StatusCode);

        var other = await SetupAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await ReviewAsync(other, fileEvidenceId, "Accepted", null)).StatusCode);
    }

    [Fact]
    public async Task Endpoints_RequireBusinessAdmin()
    {
        var setup = await SetupAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync(ParticipantsUrl(setup.ActivityId), setup.Student)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/activities/{setup.ActivityId}/sessions/{setup.SessionId}/check-ins", setup.Student,
            new { registrationId = setup.StudentRegistrationId })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync(ParticipantsUrl(setup.ActivityId), null)).StatusCode);
    }

    private sealed record Setup(string Staff, string Student, int ActivityId, int SessionId, int StudentRegistrationId, int MonitorRegistrationId);

    /// <summary>Published one-session activity (starts in 5 minutes) with SV210002 and SV210001 registered.</summary>
    private async Task<Setup> SetupAsync()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var monitor = await _api.LoginAsync(MonitorCode, StudentPassword);
        var start = DateTime.UtcNow.AddMinutes(5);
        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"Participants-{Guid.NewGuid():N}",
            description = "Kiểm thử người tham gia",
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
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var activity = await DataAsync(created);
        var activityId = activity.GetProperty("id").GetInt32();
        foreach (var token in new[] { student, monitor })
        {
            Assert.Equal(HttpStatusCode.OK,
                (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", token, null)).StatusCode);
        }

        var participants = (await DataAsync(await _api.GetAsync(ParticipantsUrl(activityId), staff)))
            .GetProperty("participants").GetProperty("items").EnumerateArray().ToList();
        int RegistrationOf(string code) => participants
            .Single(item => item.GetProperty("studentCode").GetString() == code)
            .GetProperty("registrationId").GetInt32();

        return new Setup(staff, student, activityId, activity.GetProperty("sessions")[0].GetProperty("id").GetInt32(),
            RegistrationOf(StudentCode), RegistrationOf(MonitorCode));
    }

    /// <summary>Evidence upload is US-08 (Kiệt); until then tests store evidence the same way that service will.</summary>
    private async Task<int> AddEvidenceAsync(int registrationId, bool withFile)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var evidence = new ActivityEvidence
        {
            RegistrationId = registrationId,
            EvidenceType = withFile ? EvidenceTypes.Image : EvidenceTypes.ConfirmationCode,
            ConfirmationCode = withFile ? null : "BTC-2026-001",
            UploadedAt = DateTime.UtcNow
        };
        if (withFile)
        {
            var storage = scope.ServiceProvider.GetRequiredService<IEvidenceFileStorage>();
            evidence.StoredFileName = await storage.SaveAsync(new MemoryStream(PngBytes), ".png");
            evidence.OriginalFileName = "anh-tham-gia.png";
            evidence.ContentType = "image/png";
            evidence.SizeBytes = PngBytes.Length;
        }

        context.ActivityEvidences.Add(evidence);
        await context.SaveChangesAsync();
        return evidence.Id;
    }

    private Task<HttpResponseMessage> RecordAsync(Setup setup, int registrationId) =>
        _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/activities/{setup.ActivityId}/sessions/{setup.SessionId}/check-ins",
            setup.Staff, new { registrationId });

    private Task<HttpResponseMessage> ReviewAsync(Setup setup, int evidenceId, string status, string? note) =>
        _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/activities/{setup.ActivityId}/evidences/{evidenceId}/review",
            setup.Staff, new { status, note });

    private static string UndoUrl(Setup setup, int registrationId) =>
        $"/api/v1/admin/activities/{setup.ActivityId}/sessions/{setup.SessionId}/check-ins/{registrationId}";

    private static string ParticipantsUrl(int activityId) => $"/api/v1/admin/activities/{activityId}/participants";

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
