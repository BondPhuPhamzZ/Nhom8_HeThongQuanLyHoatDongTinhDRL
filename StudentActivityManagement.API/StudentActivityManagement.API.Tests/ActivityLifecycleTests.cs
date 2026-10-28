using System.Globalization;
using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// Sprint 3 foundation, part 2: activity lifecycle (state machine), draft-only deletion,
/// Requirements field and the unified status codes.
/// </summary>
public sealed class ActivityLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string StudentCode = "SV210002";
    private const string StudentPassword = "123456";
    private readonly AccountTestClient _api;

    public ActivityLifecycleTests(CustomWebApplicationFactory factory)
    {
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task Requirements_AreSavedAndShownToStaffAndStudents()
    {
        var admin = await AdminAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var created = await CreateAsync(admin, "Published", requirements: "Mang thẻ sinh viên");
        var id = created.GetProperty("id").GetInt32();

        Assert.Equal("Mang thẻ sinh viên", created.GetProperty("requirements").GetString());
        var forStudent = await DataAsync(await _api.GetAsync($"/api/v1/student-activities/{id}", student));
        Assert.Equal("Mang thẻ sinh viên", forStudent.GetProperty("requirements").GetString());
        Assert.Equal(JsonValueKind.Null, forStudent.GetProperty("allowedNextStatuses").ValueKind);
    }

    [Theory]
    [InlineData("Ongoing")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task Create_InNonInitialStatus_Returns400(string status)
    {
        var admin = await AdminAsync();

        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", admin, Body(status));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task StatusChanges_FollowLifecycle_AndFinalStatusIsLocked()
    {
        var admin = await AdminAsync();
        var draft = await CreateAsync(admin, "Draft");
        var id = draft.GetProperty("id").GetInt32();
        Assert.Equal(new[] { "Published", "Cancelled" }, NextStatuses(draft));

        var skip = await PatchStatusAsync(admin, id, "Ongoing");
        Assert.Equal(HttpStatusCode.Conflict, skip.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(skip));

        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(admin, id, "Published")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(admin, id, "Ongoing")).StatusCode);
        var completed = await PatchStatusAsync(admin, id, "Completed");
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Empty(NextStatuses(await DataAsync(completed)));

        Assert.Equal(HttpStatusCode.Conflict, (await PatchStatusAsync(admin, id, "Published")).StatusCode);
        var edit = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/activities/{id}", admin, Body("Completed"));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
    }

    [Fact]
    public async Task Update_WithIllegalStatusJump_Returns409()
    {
        var admin = await AdminAsync();
        var id = (await CreateAsync(admin, "Draft")).GetProperty("id").GetInt32();

        var response = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/activities/{id}", admin, Body("Completed"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task BackToDraft_IsBlockedWhileStudentsAreRegistered()
    {
        var admin = await AdminAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var id = (await CreateAsync(admin, "Published")).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{id}/register", student, null)).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await PatchStatusAsync(admin, id, "Draft")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/student-activities/{id}/registration", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(admin, id, "Draft")).StatusCode);
    }

    [Fact]
    public async Task Delete_OnlyRemovesDraftsWithoutRegistrationHistory()
    {
        var admin = await AdminAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);

        var published = (await CreateAsync(admin, "Published")).GetProperty("id").GetInt32();
        var deletePublished = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/activities/{published}", admin, null);
        Assert.Equal(HttpStatusCode.Conflict, deletePublished.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(deletePublished));

        // A cancelled registration is still history: back in Draft, the activity can no longer be deleted.
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{published}/register", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/student-activities/{published}/registration", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(admin, published, "Draft")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/activities/{published}", admin, null)).StatusCode);

        var draft = (await CreateAsync(admin, "Draft")).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/activities/{draft}", admin, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/api/v1/admin/activities/{draft}", admin)).StatusCode);
    }

    [Fact]
    public async Task Register_AfterRegistrationClosed_Returns409()
    {
        var admin = await AdminAsync();
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var body = Body("Published", openTime: DateTime.UtcNow.AddHours(-3), closeTime: DateTime.UtcNow.AddHours(-1));
        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", admin, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await DataAsync(created)).GetProperty("id").GetInt32();

        var response = await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{id}/register", student, null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task CreateClass_Returns201()
    {
        var admin = await AdminAsync();
        var code = $"LT{Guid.NewGuid():N}"[..12];

        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/classes", admin,
            new { classCode = code, className = "Lớp kiểm thử", department = "CNTT" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(code, (await DataAsync(response)).GetProperty("classCode").GetString());
    }

    private Task<string> AdminAsync() => _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    private async Task<JsonElement> CreateAsync(string token, string status, string requirements = "")
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", token, Body(status, requirements));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await DataAsync(response);
    }

    private Task<HttpResponseMessage> PatchStatusAsync(string token, int id, string status) =>
        _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/activities/{id}/status", token, new { status });

    private static object Body(string status, string requirements = "", DateTime? openTime = null, DateTime? closeTime = null)
    {
        var start = DateTime.UtcNow.AddDays(3);
        return new
        {
            activityName = $"Lifecycle-{Guid.NewGuid():N}",
            description = "Kiểm thử vòng đời hoạt động",
            requirements,
            organizerName = "Phòng CTSV",
            registrationOpenTime = Iso(openTime ?? DateTime.UtcNow.AddHours(-1)),
            registrationCloseTime = Iso(closeTime ?? DateTime.UtcNow.AddHours(6)),
            maxParticipants = 10,
            trainingPoints = 5,
            targetAudience = "Tất cả sinh viên",
            status,
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường", startTime = Iso(start), endTime = Iso(start.AddHours(2)), sortOrder = 1 }
            },
            audienceRules = Array.Empty<object>()
        };
    }

    private static string[] NextStatuses(JsonElement activity) => activity.GetProperty("allowedNextStatuses")
        .EnumerateArray().Select(item => item.GetString()!).ToArray();

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
