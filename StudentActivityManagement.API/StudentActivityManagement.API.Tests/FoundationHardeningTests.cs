using System.Globalization;
using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// Sprint 3 foundation: stable session ids, UTC time contract, re-registration after cancel, Ongoing visibility.
/// </summary>
public sealed class FoundationHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string StudentCode = "SV210002";
    private const string StudentPassword = "123456";
    private readonly AccountTestClient _api;

    public FoundationHardeningTests(CustomWebApplicationFactory factory)
    {
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task UpdateActivity_WithSessionIds_KeepsIdsAcrossReorderAddAndRemove()
    {
        var admin = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var created = await CreateActivityAsync(admin, sessionCount: 2);
        var id = created.GetProperty("id").GetInt32();
        var first = SessionId(created, sortOrder: 1);
        var second = SessionId(created, sortOrder: 2);

        // Swap the two orders (would collide on the unique index if updated naively) and add a third session.
        var reordered = await UpdateAsync(admin, id, [Session(first, 2), Session(second, 1), Session(null, 3)]);
        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        var afterReorder = await DataAsync(reordered);
        Assert.Equal(3, afterReorder.GetProperty("sessions").GetArrayLength());
        Assert.Equal(first, SessionId(afterReorder, sortOrder: 2));
        Assert.Equal(second, SessionId(afterReorder, sortOrder: 1));
        var third = SessionId(afterReorder, sortOrder: 3);
        Assert.DoesNotContain(third, new[] { first, second });

        var trimmed = await UpdateAsync(admin, id, [Session(second, 1), Session(third, 2)]);
        Assert.Equal(HttpStatusCode.OK, trimmed.StatusCode);
        var afterTrim = await DataAsync(trimmed);
        Assert.Equal(new[] { second, third }.Order(),
            afterTrim.GetProperty("sessions").EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).Order());
    }

    [Fact]
    public async Task UpdateActivity_WithSessionIdOfAnotherActivity_Returns400()
    {
        var admin = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var target = await CreateActivityAsync(admin, sessionCount: 1);
        var other = await CreateActivityAsync(admin, sessionCount: 1);

        var response = await UpdateAsync(admin, target.GetProperty("id").GetInt32(), [Session(SessionId(other, 1), 1)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task CreateActivity_TimeWithoutOffset_Returns400()
    {
        var admin = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var start = DateTime.UtcNow.AddDays(3);
        var body = ActivityBody(
            Iso(DateTime.UtcNow.AddHours(-1)), Iso(DateTime.UtcNow.AddHours(6)),
            [SessionBody(null, 1, start.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture), Iso(start.AddHours(2)))]);

        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", admin, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task CreateActivity_TimeWithVietnamOffset_IsStoredAndReturnedAsUtc()
    {
        var admin = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var localStart = new DateTimeOffset(
            DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(5).AddHours(8), DateTimeKind.Unspecified), TimeSpan.FromHours(7));
        var body = ActivityBody(
            Iso(DateTime.UtcNow.AddHours(-1)), Iso(DateTime.UtcNow.AddHours(6)),
            [SessionBody(null, 1, localStart.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                localStart.AddHours(2).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture))]);

        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", admin, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await DataAsync(response);

        var fetched = await DataAsync(await _api.GetAsync($"/api/v1/admin/activities/{created.GetProperty("id").GetInt32()}", admin));
        var startText = fetched.GetProperty("sessions")[0].GetProperty("startTime").GetString()!;
        Assert.EndsWith("Z", startText);
        Assert.Equal(localStart.UtcDateTime, DateTime.Parse(startText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
    }

    [Fact]
    public async Task Register_AfterCancel_ReactivatesSameRegistrationAndCapacity()
    {
        var admin = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var id = (await CreateActivityAsync(admin, sessionCount: 1)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{id}/register", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/student-activities/{id}/registration", student, null)).StatusCode);
        var cancelled = await DataAsync(await _api.GetAsync($"/api/v1/student-activities/{id}", student));
        Assert.Equal("Cancelled", cancelled.GetProperty("myRegistrationStatus").GetString());

        var again = await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{id}/register", student, null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var detail = await DataAsync(await _api.GetAsync($"/api/v1/student-activities/{id}", student));
        Assert.Equal("Registered", detail.GetProperty("myRegistrationStatus").GetString());
        Assert.Equal(1, detail.GetProperty("currentParticipantsCount").GetInt32());

        var duplicate = await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{id}/register", student, null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task OngoingActivity_IsVisibleToStudents_ButNotOpenForRegistration()
    {
        var admin = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var student = await _api.LoginAsync(StudentCode, StudentPassword);
        var created = await CreateActivityAsync(admin, sessionCount: 1);
        var id = created.GetProperty("id").GetInt32();
        var name = created.GetProperty("activityName").GetString()!;
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/activities/{id}/status", admin, new { status = "Ongoing" })).StatusCode);

        var list = await DataAsync(await _api.GetAsync($"/api/v1/student-activities?search={Uri.EscapeDataString(name)}", student));
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync($"/api/v1/student-activities/{id}", student)).StatusCode);

        var register = await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{id}/register", student, null);
        Assert.Equal(HttpStatusCode.Conflict, register.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(register));
    }

    private async Task<JsonElement> CreateActivityAsync(string token, int sessionCount)
    {
        var start = DateTime.UtcNow.AddDays(2);
        var sessions = Enumerable.Range(1, sessionCount)
            .Select(order => SessionBody(null, order, Iso(start.AddDays(order)), Iso(start.AddDays(order).AddHours(2))))
            .ToArray();
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", token,
            ActivityBody(Iso(DateTime.UtcNow.AddHours(-1)), Iso(DateTime.UtcNow.AddHours(6)), sessions));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await DataAsync(response);
    }

    private Task<HttpResponseMessage> UpdateAsync(string token, int id, object[] sessions) =>
        _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/activities/{id}", token,
            ActivityBody(Iso(DateTime.UtcNow.AddHours(-1)), Iso(DateTime.UtcNow.AddHours(6)), sessions));

    private static object Session(int? id, int sortOrder)
    {
        var start = DateTime.UtcNow.AddDays(2 + sortOrder);
        return SessionBody(id, sortOrder, Iso(start), Iso(start.AddHours(2)));
    }

    private static object SessionBody(int? id, int sortOrder, string startTime, string endTime) => new
    {
        id,
        sessionName = $"Buổi {sortOrder}",
        campus = "Cơ sở 1",
        location = $"Phòng {sortOrder}",
        startTime,
        endTime,
        sortOrder
    };

    private static object ActivityBody(string registrationOpenTime, string registrationCloseTime, object[] sessions) => new
    {
        activityName = $"Foundation-{Guid.NewGuid():N}",
        description = "Kiểm thử củng cố nền",
        organizerName = "Phòng CTSV",
        registrationOpenTime,
        registrationCloseTime,
        maxParticipants = 10,
        trainingPoints = 5,
        targetAudience = "Tất cả sinh viên",
        status = "Published",
        sessions,
        audienceRules = Array.Empty<object>()
    };

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static int SessionId(JsonElement activity, int sortOrder) => activity.GetProperty("sessions").EnumerateArray()
        .First(session => session.GetProperty("sortOrder").GetInt32() == sortOrder).GetProperty("id").GetInt32();

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
