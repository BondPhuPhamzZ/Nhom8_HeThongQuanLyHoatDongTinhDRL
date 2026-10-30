using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Storage;

namespace StudentActivityManagement.API.Tests;

public sealed class EvidenceUploadTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string StudentCode = "SV210002";
    private const string MonitorCode = "SV210001";
    private const string StudentPassword = "123456";

    private static readonly byte[] ValidPngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] ValidPdfBytes = "%PDF-1.4\nTest content"u8.ToArray();

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly AccountTestClient _api;

    public EvidenceUploadTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        _api = new AccountTestClient(_httpClient);
    }

    [Fact]
    public async Task Upload_Image_ReturnsCreatedAndListsIt()
    {
        var setup = await SetupOngoingAsync();

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "Image",
            fileBytes: ValidPngBytes,
            fileName: "test.png",
            contentType: "image/png",
            sessionId: setup.SessionId,
            note: "Minh chứng ảnh tham gia");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/v1/student-activities/{setup.ActivityId}/evidences", response.Headers.Location?.ToString());

        var json = await ReadDataAsync(response);
        var evidenceId = json.GetProperty("id").GetInt32();
        Assert.True(evidenceId > 0);
        Assert.Equal(setup.ActivityId, json.GetProperty("activityId").GetInt32());
        Assert.Equal(setup.SessionId, json.GetProperty("sessionId").GetInt32());
        Assert.Equal("Buổi 1", json.GetProperty("sessionName").GetString());
        Assert.Equal("Image", json.GetProperty("evidenceType").GetString());
        Assert.Equal("test.png", json.GetProperty("originalFileName").GetString());
        Assert.Equal("image/png", json.GetProperty("contentType").GetString());
        Assert.Equal(ValidPngBytes.Length, json.GetProperty("sizeBytes").GetInt64());
        Assert.Equal($"/api/v1/student-activities/{setup.ActivityId}/evidences/{evidenceId}/file", json.GetProperty("fileUrl").GetString());
        Assert.Equal("Pending", json.GetProperty("reviewStatus").GetString());
        Assert.True(json.GetProperty("canDelete").GetBoolean());

        // AC2: Never expose storedFileName, registrationId or physical path
        Assert.False(json.TryGetProperty("storedFileName", out _));
        Assert.False(json.TryGetProperty("registrationId", out _));

        // GET list verifies it is listed
        var listResponse = await _api.GetAsync(EvidencesUrl(setup.ActivityId), setup.StudentToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listData = await ReadDataAsync(listResponse);
        Assert.Equal(1, listData.GetArrayLength());
        var first = listData[0];
        Assert.Equal(evidenceId, first.GetProperty("id").GetInt32());
        Assert.False(first.TryGetProperty("storedFileName", out _));
        Assert.False(first.TryGetProperty("registrationId", out _));
    }

    [Fact]
    public async Task Upload_ConfirmationCode_WithoutFile_ReturnsCreated()
    {
        var setup = await SetupOngoingAsync();

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-9988",
            note: "Mã BTC cấp");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await ReadDataAsync(response);
        Assert.Equal("ConfirmationCode", json.GetProperty("evidenceType").GetString());
        Assert.Equal("CONF-9988", json.GetProperty("confirmationCode").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("fileUrl").ValueKind);
    }

    [Fact]
    public async Task Upload_FakePngExtension_ReturnsValidationFailed()
    {
        var setup = await SetupOngoingAsync();
        var fakeBytes = "Hello this is a plain text file"u8.ToArray();

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "Image",
            fileBytes: fakeBytes,
            fileName: "fake.png",
            contentType: "image/png");

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Upload_TooLarge_ReturnsValidationFailed()
    {
        var setup = await SetupOngoingAsync();

        // 5 MB + 1 byte
        var largeBytes = new byte[5 * 1024 * 1024 + 1];
        Array.Copy(ValidPngBytes, largeBytes, ValidPngBytes.Length);

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "Image",
            fileBytes: largeBytes,
            fileName: "toolarge.png",
            contentType: "image/png");

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Upload_CodeTypeWithFile_ReturnsValidationFailed()
    {
        var setup = await SetupOngoingAsync();

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "ConfirmationCode",
            fileBytes: ValidPngBytes,
            fileName: "extra.png",
            confirmationCode: "CONF-1234");

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Upload_ImageWithoutFile_ReturnsValidationFailed()
    {
        var setup = await SetupOngoingAsync();

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "Image",
            confirmationCode: "SOMECODE");

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Upload_SessionOfOtherActivity_ReturnsValidationFailed()
    {
        var setup1 = await SetupOngoingAsync();
        var setup2 = await SetupOngoingAsync();

        var response = await PostMultipartAsync(
            EvidencesUrl(setup1.ActivityId),
            setup1.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-1234",
            sessionId: setup2.SessionId); // Session belongs to setup2

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Upload_BeforeActivityStarts_ReturnsConflict()
    {
        // Activity is Published, not Ongoing
        var setup = await SetupPublishedAsync();

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-1234");

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.Conflict, ApiErrorCodes.BusinessRuleViolation);
    }

    [Fact]
    public async Task Upload_AfterCancellingRegistration_ReturnsConflict()
    {
        var setup = await SetupOngoingAsync();

        // Cancel registration
        var cancelResponse = await _api.SendAsync(
            HttpMethod.Delete,
            $"/api/v1/student-activities/{setup.ActivityId}/registration",
            setup.StudentToken,
            null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-1234");

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.Conflict, ApiErrorCodes.BusinessRuleViolation);
    }

    [Fact]
    public async Task Upload_SixthEvidence_ReturnsConflict()
    {
        var setup = await SetupOngoingAsync();

        // Upload 5 valid confirmation codes
        for (var i = 1; i <= 5; i++)
        {
            var res = await PostMultipartAsync(
                EvidencesUrl(setup.ActivityId),
                setup.StudentToken,
                evidenceType: "ConfirmationCode",
                confirmationCode: $"CONF-100{i}");
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        // 6th upload returns Conflict
        var sixth = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-1006");

        await AccountTestClient.AssertProblemAsync(sixth, HttpStatusCode.Conflict, ApiErrorCodes.BusinessRuleViolation);

        // Concurrency test: with a new activity having 4 evidences, 2 simultaneous uploads should not exceed 5
        var setup2 = await SetupOngoingAsync();
        for (var i = 1; i <= 4; i++)
        {
            var res = await PostMultipartAsync(
                EvidencesUrl(setup2.ActivityId),
                setup2.StudentToken,
                evidenceType: "ConfirmationCode",
                confirmationCode: $"CONF-200{i}");
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        var taskA = PostMultipartAsync(
            EvidencesUrl(setup2.ActivityId),
            setup2.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-2005A");
        var taskB = PostMultipartAsync(
            EvidencesUrl(setup2.ActivityId),
            setup2.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-2005B");

        var results = await Task.WhenAll(taskA, taskB);
        var successCount = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(2, successCount + conflictCount);
        Assert.True(successCount <= 1);

        // Verify total in DB is at most 5
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var total = await context.ActivityEvidences.CountAsync(e => e.RegistrationId == setup2.StudentRegistrationId);
        Assert.True(total <= 5);
    }

    [Fact]
    public async Task Upload_NotRegistered_ReturnsNotFound()
    {
        var setup = await SetupOngoingAsync();

        // SV210001 (Monitor) didn't register in this setup variant if we use a non-registered token or another user
        var staffToken = setup.StaffToken; // Not a student
        var unregisteredStudentToken = await _api.LoginAsync(MonitorCode, StudentPassword);

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            unregisteredStudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-1234");

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.NotFound, ApiErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task Download_OwnFile_ReturnsSameBytes_AndOtherStudentGetsNotFound()
    {
        var setup = await SetupOngoingAsync(registerMonitor: true);

        var uploadResponse = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "Image",
            fileBytes: ValidPngBytes,
            fileName: "photo.png",
            contentType: "image/png");
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var evidenceId = (await ReadDataAsync(uploadResponse)).GetProperty("id").GetInt32();

        // Own download
        var ownDownload = await _api.GetAsync(
            $"/api/v1/student-activities/{setup.ActivityId}/evidences/{evidenceId}/file",
            setup.StudentToken);
        Assert.Equal(HttpStatusCode.OK, ownDownload.StatusCode);
        Assert.Equal("image/png", ownDownload.Content.Headers.ContentType?.MediaType);
        var downloadedBytes = await ownDownload.Content.ReadAsByteArrayAsync();
        Assert.Equal(ValidPngBytes, downloadedBytes);

        // Other student (Monitor) gets 404
        var otherDownload = await _api.GetAsync(
            $"/api/v1/student-activities/{setup.ActivityId}/evidences/{evidenceId}/file",
            setup.MonitorToken);
        await AccountTestClient.AssertProblemAsync(otherDownload, HttpStatusCode.NotFound, ApiErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task Delete_Pending_RemovesRecordAndFile_AndAcceptedReturnsConflict()
    {
        var setup = await SetupOngoingAsync();

        var upload = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "Image",
            fileBytes: ValidPngBytes,
            fileName: "delete-me.png",
            contentType: "image/png");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var evidenceId = (await ReadDataAsync(upload)).GetProperty("id").GetInt32();

        // Get stored file name from DB to verify physical file deletion
        string storedFileName;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var record = await context.ActivityEvidences.FindAsync(evidenceId);
            Assert.NotNull(record);
            storedFileName = record.StoredFileName!;
        }

        // Delete while Pending
        var deleteRes = await _api.SendAsync(
            HttpMethod.Delete,
            $"/api/v1/student-activities/{setup.ActivityId}/evidences/{evidenceId}",
            setup.StudentToken,
            null);
        Assert.Equal(HttpStatusCode.OK, deleteRes.StatusCode);

        // Verify DB record removed
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var record = await context.ActivityEvidences.FindAsync(evidenceId);
            Assert.Null(record);

            var storage = scope.ServiceProvider.GetRequiredService<IEvidenceFileStorage>();
            var stream = await storage.OpenReadAsync(storedFileName);
            Assert.Null(stream);
        }

        // Now upload another one and review as Accepted by HSSV
        var upload2 = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-9999");
        var evidence2Id = (await ReadDataAsync(upload2)).GetProperty("id").GetInt32();

        // HSSV reviews evidence via US-21 API
        var reviewRes = await _api.SendAsync(
            HttpMethod.Patch,
            $"/api/v1/admin/activities/{setup.ActivityId}/evidences/{evidence2Id}/review",
            setup.StaffToken,
            new { status = "Accepted" });
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);

        // Student tries to delete Accepted evidence -> Conflict 409
        var deleteAcceptedRes = await _api.SendAsync(
            HttpMethod.Delete,
            $"/api/v1/student-activities/{setup.ActivityId}/evidences/{evidence2Id}",
            setup.StudentToken,
            null);
        await AccountTestClient.AssertProblemAsync(deleteAcceptedRes, HttpStatusCode.Conflict, ApiErrorCodes.BusinessRuleViolation);
    }

    [Fact]
    public async Task Integration_US08_US21_Flow()
    {
        var setup = await SetupOngoingAsync();

        // 1. Student uploads file evidence via US-08
        var uploadRes = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "Image",
            fileBytes: ValidPngBytes,
            fileName: "evidence.png",
            contentType: "image/png",
            note: "Minh chứng tham gia");
        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);
        var evidenceId = (await ReadDataAsync(uploadRes)).GetProperty("id").GetInt32();

        // 2. HSSV checks participant list (US-21) and sees pending evidence
        var participantsRes = await _api.GetAsync(
            $"/api/v1/admin/activities/{setup.ActivityId}/participants?hasPendingEvidence=true",
            setup.StaffToken);
        Assert.Equal(HttpStatusCode.OK, participantsRes.StatusCode);
        var partData = await ReadDataAsync(participantsRes);
        Assert.True(partData.GetProperty("summary").GetProperty("pendingEvidence").GetInt32() >= 1);

        // 3. HSSV downloads evidence file (US-21)
        var downloadRes = await _api.GetAsync(
            $"/api/v1/admin/activities/{setup.ActivityId}/evidences/{evidenceId}/file",
            setup.StaffToken);
        Assert.Equal(HttpStatusCode.OK, downloadRes.StatusCode);
        Assert.Equal(ValidPngBytes, await downloadRes.Content.ReadAsByteArrayAsync());

        // 4. HSSV reviews evidence as Accepted (US-21)
        var reviewRes = await _api.SendAsync(
            HttpMethod.Patch,
            $"/api/v1/admin/activities/{setup.ActivityId}/evidences/{evidenceId}/review",
            setup.StaffToken,
            new { status = "Accepted", note = "Hợp lệ" });
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);

        // 5. Student GET sees new status Accepted and canDelete == false
        var studentListRes = await _api.GetAsync(EvidencesUrl(setup.ActivityId), setup.StudentToken);
        Assert.Equal(HttpStatusCode.OK, studentListRes.StatusCode);
        var studentData = await ReadDataAsync(studentListRes);
        var item = studentData.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == evidenceId);
        Assert.Equal("Accepted", item.GetProperty("reviewStatus").GetString());
        Assert.Equal("Hợp lệ", item.GetProperty("reviewNote").GetString());
        Assert.False(item.GetProperty("canDelete").GetBoolean());

        // 6. Student cannot delete accepted evidence
        var deleteRes = await _api.SendAsync(
            HttpMethod.Delete,
            $"/api/v1/student-activities/{setup.ActivityId}/evidences/{evidenceId}",
            setup.StudentToken,
            null);
        await AccountTestClient.AssertProblemAsync(deleteRes, HttpStatusCode.Conflict, ApiErrorCodes.BusinessRuleViolation);
    }

    [Fact]
    public async Task StaffCallsStudentEndpoint_ReturnsForbidden_AndNoTokenReturnsUnauthorized()
    {
        var setup = await SetupOngoingAsync();

        // Staff calls student endpoint -> 403 Forbidden
        var staffCall = await _api.GetAsync(EvidencesUrl(setup.ActivityId), setup.StaffToken);
        await AccountTestClient.AssertProblemAsync(staffCall, HttpStatusCode.Forbidden, ApiErrorCodes.AccessDenied);

        // Anonymous call -> 401 Unauthorized
        var anonCall = await _api.GetAsync(EvidencesUrl(setup.ActivityId), null);
        await AccountTestClient.AssertProblemAsync(anonCall, HttpStatusCode.Unauthorized, ApiErrorCodes.AuthenticationRequired);
    }

    [Fact]
    public async Task Upload_PastSubmissionWindow_ReturnsConflict()
    {
        var setup = await SetupOngoingAsync();

        // Modify session end time in DB to > 7 days ago
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await context.ActivitySessions.FindAsync(setup.SessionId);
            Assert.NotNull(session);
            session.StartTime = DateTime.UtcNow.AddDays(-10);
            session.EndTime = DateTime.UtcNow.AddDays(-8);

            var activity = await context.Activities.FindAsync(setup.ActivityId);
            Assert.NotNull(activity);
            activity.StartTime = DateTime.UtcNow.AddDays(-10);
            activity.EndTime = DateTime.UtcNow.AddDays(-8);

            await context.SaveChangesAsync();
        }

        var response = await PostMultipartAsync(
            EvidencesUrl(setup.ActivityId),
            setup.StudentToken,
            evidenceType: "ConfirmationCode",
            confirmationCode: "CONF-1234");

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.Conflict, ApiErrorCodes.BusinessRuleViolation);
    }

    private static string EvidencesUrl(int activityId) => $"/api/v1/student-activities/{activityId}/evidences";

    private sealed record TestSetup(
        string StaffToken,
        string StudentToken,
        string MonitorToken,
        int ActivityId,
        int SessionId,
        int StudentRegistrationId);

    private async Task<TestSetup> SetupPublishedAsync(bool registerMonitor = false)
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var monitor = await _api.LoginAsync(MonitorCode, StudentPassword);

        var start = DateTime.UtcNow.AddHours(2);
        var createResponse = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"Evidence-{Guid.NewGuid():N}",
            description = "Kiểm thử nộp minh chứng",
            organizerName = "Phòng QL HSSV",
            registrationOpenTime = DateTime.UtcNow.AddHours(-1).ToString("o"),
            registrationCloseTime = DateTime.UtcNow.AddHours(1).ToString("o"),
            maxParticipants = 50,
            trainingPoints = 5,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "A101", startTime = start.ToString("o"), endTime = start.AddHours(2).ToString("o"), sortOrder = 1 }
            },
            audienceRules = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var actData = await ReadDataAsync(createResponse);
        var activityId = actData.GetProperty("id").GetInt32();
        var sessionId = actData.GetProperty("sessions")[0].GetProperty("id").GetInt32();

        // Student registers
        var regRes = await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", student, null);
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);

        if (registerMonitor)
        {
            var monRes = await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", monitor, null);
            Assert.Equal(HttpStatusCode.OK, monRes.StatusCode);
        }

        // Get student registration id
        int studentRegId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reg = await context.ActivityRegistrations
                .FirstAsync(r => r.ActivityId == activityId && r.Student!.StudentCode == StudentCode);
            studentRegId = reg.Id;
        }

        return new TestSetup(staff, student, monitor, activityId, sessionId, studentRegId);
    }

    private async Task<TestSetup> SetupOngoingAsync(bool registerMonitor = false)
    {
        var setup = await SetupPublishedAsync(registerMonitor);

        // HSSV switches activity to Ongoing
        var patchResponse = await _api.SendAsync(
            HttpMethod.Patch,
            $"/api/v1/admin/activities/{setup.ActivityId}/status",
            setup.StaffToken,
            new { status = "Ongoing" });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        return setup;
    }

    private async Task<HttpResponseMessage> PostMultipartAsync(
        string url,
        string? token,
        string evidenceType,
        byte[]? fileBytes = null,
        string? fileName = null,
        string? contentType = null,
        string? confirmationCode = null,
        int? sessionId = null,
        string? note = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(evidenceType), "evidenceType");

        if (fileBytes != null && fileName != null)
        {
            var fileContent = new ByteArrayContent(fileBytes);
            if (contentType != null)
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
            content.Add(fileContent, "file", fileName);
        }

        if (confirmationCode != null)
        {
            content.Add(new StringContent(confirmationCode), "confirmationCode");
        }

        if (sessionId.HasValue)
        {
            content.Add(new StringContent(sessionId.Value.ToString()), "sessionId");
        }

        if (note != null)
        {
            content.Add(new StringContent(note), "note");
        }

        request.Content = content;
        return await _httpClient.SendAsync(request);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await AccountTestClient.ReadJsonAsync(response);
        return json.GetProperty("data").Clone();
    }
}
