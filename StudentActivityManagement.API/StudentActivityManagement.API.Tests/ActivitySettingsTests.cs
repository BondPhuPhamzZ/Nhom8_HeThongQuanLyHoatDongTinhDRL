using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Tests;

public sealed class ActivitySettingsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public ActivitySettingsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task CheckInConfig_Defaults_UpsertsAndFeedsSharedProvider()
    {
        var staff = await StaffAsync();
        var activityId = await CreateActivityAsync(staff);
        var url = ConfigUrl(activityId);

        var defaults = await DataAsync(await _api.GetAsync(url, staff));
        Assert.Equal("Code", defaults.GetProperty("method").GetString());
        Assert.Equal(15, defaults.GetProperty("openBeforeMinutes").GetInt32());
        Assert.Equal(30, defaults.GetProperty("closeAfterMinutes").GetInt32());
        Assert.True(defaults.GetProperty("isDefault").GetBoolean());

        var saved = await _api.SendAsync(HttpMethod.Put, url, staff,
            new { method = "Manual", openBeforeMinutes = 0, closeAfterMinutes = 240 });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var data = await DataAsync(saved);
        Assert.Equal("Manual", data.GetProperty("method").GetString());
        Assert.False(data.GetProperty("isDefault").GetBoolean());
        Assert.EndsWith("Z", data.GetProperty("updatedAt").GetString());

        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ICheckInConfigProvider>();
        var effective = await provider.GetAsync(activityId);
        Assert.Equal("Manual", effective.Method);
        Assert.Equal(0, effective.OpenBeforeMinutes);
        Assert.Equal(240, effective.CloseAfterMinutes);
        Assert.False(effective.IsDefault);
    }

    [Fact]
    public async Task CheckInConfig_ValidatesStatusAndMethodLock()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var activityId = await CreateActivityAsync(staff, student);
        var url = ConfigUrl(activityId);

        await AccountTestClient.AssertProblemAsync(
            await _api.SendAsync(HttpMethod.Put, url, staff, new { method = "GPS", openBeforeMinutes = 15, closeAfterMinutes = 30 }),
            HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _api.SendAsync(HttpMethod.Put, url, staff, new { method = "Code", openBeforeMinutes = 241, closeAfterMinutes = 30 })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, url, staff,
            new { method = "Code", openBeforeMinutes = 15, closeAfterMinutes = 30 })).StatusCode);
        await RecordCheckInAsync(activityId, student);

        Assert.Equal(HttpStatusCode.Conflict,
            (await _api.SendAsync(HttpMethod.Put, url, staff, new { method = "Manual", openBeforeMinutes = 15, closeAfterMinutes = 30 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Put, url, staff, new { method = "Code", openBeforeMinutes = 20, closeAfterMinutes = 45 })).StatusCode);
    }

    [Fact]
    public async Task ReminderSchedule_DefaultsReplaceSortAndRejectInvalidValues()
    {
        var staff = await StaffAsync();
        var activityId = await CreateActivityAsync(staff);
        var url = ReminderUrl(activityId);

        var defaults = await DataAsync(await _api.GetAsync(url, staff));
        Assert.True(defaults.GetProperty("isDefault").GetBoolean());
        Assert.Equal(new[] { 1440 }, defaults.GetProperty("offsetsMinutes").EnumerateArray().Select(item => item.GetInt32()));

        var saved = await _api.SendAsync(HttpMethod.Put, url, staff, new { offsetsMinutes = new[] { 1440, 60 } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(new[] { 60, 1440 }, (await DataAsync(saved)).GetProperty("offsetsMinutes").EnumerateArray().Select(item => item.GetInt32()));
        Assert.False((await DataAsync(await _api.GetAsync(url, staff))).GetProperty("isDefault").GetBoolean());

        foreach (var offsets in new[] { Array.Empty<int>(), new[] { 5 }, new[] { 60, 60 }, new[] { 60, 120, 180, 240 }, new[] { 20000 } })
        {
            var invalid = await _api.SendAsync(HttpMethod.Put, url, staff, new { offsetsMinutes = offsets });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }

    [Fact]
    public async Task Settings_EnforceRoleAndActivityLifecycle()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var activityId = await CreateActivityAsync(staff);
        var url = ConfigUrl(activityId);

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync(url, student)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(ConfigUrl(int.MaxValue), staff)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
            $"/api/v1/admin/activities/{activityId}/status", staff, new { status = "Cancelled" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _api.SendAsync(HttpMethod.Put, url, staff, new { method = "Code", openBeforeMinutes = 15, closeAfterMinutes = 30 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _api.SendAsync(HttpMethod.Put, ReminderUrl(activityId), staff, new { offsetsMinutes = new[] { 60 } })).StatusCode);
    }

    private Task<string> StaffAsync() => _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    private async Task<int> CreateActivityAsync(string staff, string? student = null)
    {
        var now = DateTime.UtcNow;
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"Settings-{Guid.NewGuid():N}",
            description = "Kiểm thử cấu hình hoạt động",
            organizerName = "Phòng CTSV",
            registrationOpenTime = Iso(now.AddHours(-1)),
            registrationCloseTime = Iso(now.AddHours(1)),
            maxParticipants = 10,
            trainingPoints = 5,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường", startTime = Iso(now.AddHours(2)), endTime = Iso(now.AddHours(3)), sortOrder = 1 }
            },
            audienceRules = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activityId = (await DataAsync(response)).GetProperty("id").GetInt32();
        if (student != null)
        {
            Assert.Equal(HttpStatusCode.OK,
                (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", student, null)).StatusCode);
        }
        return activityId;
    }

    private async Task RecordCheckInAsync(int activityId, string student)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registration = await context.ActivityRegistrations.SingleAsync(item => item.ActivityId == activityId);
        var session = await context.ActivitySessions.SingleAsync(item => item.ActivityId == activityId);
        context.ActivityCheckIns.Add(new ActivityCheckIn
        {
            RegistrationId = registration.Id,
            SessionId = session.Id,
            Method = CheckInMethods.Code,
            CheckedInAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static string ConfigUrl(int activityId) => $"/api/v1/admin/activities/{activityId}/check-in-config";
    private static string ReminderUrl(int activityId) => $"/api/v1/admin/activities/{activityId}/reminder-schedule";
    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
