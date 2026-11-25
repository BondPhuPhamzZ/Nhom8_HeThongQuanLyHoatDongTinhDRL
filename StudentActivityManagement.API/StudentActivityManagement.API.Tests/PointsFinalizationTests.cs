using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Tests;

public sealed class PointsFinalizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public PointsFinalizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task Worker_FinalizesOnSchedule_AndPreservesCancelledRegistration()
    {
        var setup = await CreateAsync();
        var periodId = await FindOrCreatePeriodAsync(setup, true);
        var code = $"FIX{Guid.NewGuid():N}"[..12];
        Assert.True((await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/students", setup.Staff, new
        {
            studentCode = code, fullName = "Sinh viên hủy đăng ký", email = $"{code}@student.test",
            password = "123456", campus = "Cơ sở 1", academicYear = "K21"
        })).IsSuccessStatusCode);
        var cancelled = await _api.LoginAsync(code, "123456");
        await RegisterAsync(setup, cancelled);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete,
            $"/api/v1/student-activities/{setup.Id}/registration", cancelled, null)).StatusCode);

        var checkIn = await RecordAsync(setup);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var worker = scope.ServiceProvider.GetRequiredService<IActivityLifecycleService>();
        Assert.False(await db.PointRecords.AnyAsync(item => item.ActivityId == setup.Id));

        var activity = await DataAsync(await _api.GetAsync($"/api/v1/admin/activities/{setup.Id}", setup.Staff));
        var due = activity.GetProperty("pointsFinalizeDueAt").GetDateTime();
        Assert.Equal(activity.GetProperty("checkInClosesAt").GetDateTime().AddDays(1), due);
        var history = await DataAsync(await _api.GetAsync($"/api/v1/student/reports/activity-history?periodId={periodId}", setup.Student));
        var row = history.GetProperty("activities").EnumerateArray().Single(item => item.GetProperty("activityId").GetInt32() == setup.Id);
        Assert.StartsWith("Chờ chốt điểm", row.GetProperty("pointStatusLabel").GetString());
        Assert.Equal(due, row.GetProperty("pointsFinalizeDueAt").GetDateTime());

        using (_factory.Time.At(due.AddMinutes(-1)))
        {
            await worker.RunOnceAsync();
            Assert.False(await db.PointRecords.AnyAsync(item => item.ActivityId == setup.Id));
        }

        using var clock = _factory.Time.At(due.AddSeconds(1));
        await worker.RunOnceAsync();
        await worker.RunOnceAsync();
        Assert.Equal(HttpStatusCode.OK, (await FinalizeAsync(setup)).StatusCode);
        var records = await db.PointRecords.AsNoTracking().Include(item => item.History)
            .Where(item => item.ActivityId == setup.Id).ToListAsync();
        Assert.Equal(2, records.Count);
        var approved = Assert.Single(records, item => item.Status == PointRecordStatus.Approved);
        var absent = Assert.Single(records, item => item.Status == PointRecordStatus.Rejected);
        Assert.Equal(3, approved.Points);
        Assert.Equal(3, absent.Points);
        Assert.Equal(AttendancePoints.AbsentReason, absent.Reason);
        Assert.All(records, item => Assert.Null(Assert.Single(item.History).ChangedById));
        var registrations = await db.ActivityRegistrations.AsNoTracking().Where(item => item.ActivityId == setup.Id).ToListAsync();
        Assert.Equal(RegistrationStatus.Absent, registrations.Single(item => item.Id == absent.RegistrationId).Status);
        Assert.Equal(0, registrations.Single(item => item.Id == absent.RegistrationId).PointsEarned);
        Assert.Equal(3, registrations.Single(item => item.Id == approved.RegistrationId).PointsEarned);
        Assert.Single(registrations, item => item.Status == RegistrationStatus.Cancelled);
        Assert.Equal(2, await db.UserNotifications.CountAsync(item => item.ActivityId == setup.Id && item.Kind == "PointsFinalized"));
    }

    [Fact]
    public async Task ManualFinalize_BlocksEarly_AndConcurrentRequestsCreateOneDecisionPerRegistration()
    {
        var setup = await CreateAsync();
        await FindOrCreatePeriodAsync(setup, true);
        Assert.Equal(HttpStatusCode.OK, (await RecordAsync(setup)).StatusCode);
        using (_factory.Time.At(setup.Start.AddSeconds(1)))
            await AccountTestClient.AssertProblemAsync(await FinalizeAsync(setup), HttpStatusCode.Conflict, "finalization_blocked");

        using var clock = _factory.Time.At(setup.Close.AddSeconds(1));
        var responses = await Task.WhenAll(FinalizeAsync(setup), FinalizeAsync(setup));
        Assert.All(responses, item => Assert.Equal(HttpStatusCode.OK, item.StatusCode));
        var results = await Task.WhenAll(responses.Select(DataAsync));
        Assert.Equal(1, results.Sum(item => item.GetProperty("approvedCount").GetInt32()));
        Assert.Equal(1, results.Sum(item => item.GetProperty("absentCount").GetInt32()));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.PointRecords.CountAsync(item => item.ActivityId == setup.Id));
        Assert.Equal(2, await db.UserNotifications.CountAsync(item => item.ActivityId == setup.Id && item.Kind == "PointsFinalized"));
    }

    [Fact]
    public async Task ClosedPeriod_BlocksFinalizationOncePerStaff_AndWorkerRetriesAfterReopening()
    {
        var setup = await CreateAsync();
        var periodId = await FindOrCreatePeriodAsync(setup, false);
        Assert.Equal(HttpStatusCode.OK, (await RecordAsync(setup)).StatusCode);

        using var clock = _factory.Time.At(setup.Close.AddDays(1).AddSeconds(1));
        try
        {
            await AccountTestClient.AssertProblemAsync(await FinalizeAsync(setup), HttpStatusCode.Conflict, "finalization_blocked");
            await AccountTestClient.AssertProblemAsync(await FinalizeAsync(setup), HttpStatusCode.Conflict, "finalization_blocked");
            var activity = await DataAsync(await _api.GetAsync($"/api/v1/student-activities/{setup.Id}", setup.Student));
            Assert.True(activity.GetProperty("pointsFinalizationBlocked").GetBoolean());
            var history = await DataAsync(await _api.GetAsync($"/api/v1/student/reports/activity-history?periodId={periodId}", setup.Student));
            var row = history.GetProperty("activities").EnumerateArray().Single(item => item.GetProperty("activityId").GetInt32() == setup.Id);
            Assert.StartsWith("Đang chờ", row.GetProperty("pointStatusLabel").GetString());

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var staffIds = await db.Users.Where(item => item.IsActive && Role.StaffRoles.Contains(item.Role))
                .Select(item => item.Id).ToListAsync();
            var receivers = await db.UserNotifications.Where(item => item.ActivityId == setup.Id && item.Kind == "FinalizationBlocked")
                .Select(item => item.RecipientUserId).ToListAsync();
            Assert.Equal(staffIds.Order(), receivers.Order());
            var staffInbox = await DataAsync(await _api.GetAsync("/api/v1/admin/notifications", setup.Staff));
            Assert.Single(staffInbox.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("activityId").GetInt32() == setup.Id && item.GetProperty("kind").GetString() == "FinalizationBlocked");
            Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync("/api/v1/admin/notifications", setup.Student)).StatusCode);
            Assert.False(await db.PointRecords.AnyAsync(item => item.ActivityId == setup.Id));

            await SetPeriodStatusAsync(setup, periodId, true);
            await scope.ServiceProvider.GetRequiredService<IActivityLifecycleService>().RunOnceAsync();
            Assert.Equal(2, await db.PointRecords.CountAsync(item => item.ActivityId == setup.Id));
        }
        finally
        {
            await SetPeriodStatusAsync(setup, periodId, true);
        }
    }

    private async Task<Fixture> CreateAsync()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync("SV210002", "123456");
        var absent = await _api.LoginAsync("SV210001", "123456");
        var now = _factory.Time.GetUtcNow();
        var start = now.AddMinutes(5);
        var activity = await DataAsync(await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"Finalize-{Guid.NewGuid():N}", organizerName = "Phòng CTSV",
            registrationOpenTime = now.AddHours(-1), registrationCloseTime = start.AddMinutes(-1),
            maxParticipants = 12, trainingPoints = 3, status = ActivityStatus.Published,
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường", startTime = start,
                    endTime = start.AddMinutes(30), sortOrder = 1 }
            }
        }));
        var setup = new Fixture(staff, student, activity.GetProperty("id").GetInt32(), activity.GetProperty("sessions")[0].GetProperty("id").GetInt32(),
            activity.GetProperty("startTime").GetDateTime(), activity.GetProperty("checkInClosesAt").GetDateTime());
        await RegisterAsync(setup, student);
        await RegisterAsync(setup, absent);
        return setup;
    }

    private async Task<int> FindOrCreatePeriodAsync(Fixture setup, bool open)
    {
        var periods = await DataAsync(await _api.GetAsync("/api/v1/admin/evaluation-periods", setup.Staff));
        var period = periods.EnumerateArray().FirstOrDefault(item => item.GetProperty("name").GetString() == "Kỳ chốt điểm");
        var id = period.ValueKind != JsonValueKind.Undefined ? period.GetProperty("id").GetInt32() :
            (await DataAsync(await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/evaluation-periods", setup.Staff, new
            {
                name = "Kỳ chốt điểm", startDate = setup.Start.AddDays(-2), endDate = setup.Close.AddDays(4), targetPoints = 20
            }))).GetProperty("id").GetInt32();
        await SetPeriodStatusAsync(setup, id, open);
        return id;
    }

    private async Task SetPeriodStatusAsync(Fixture setup, int id, bool open)
    {
        var response = await _api.SendAsync(HttpMethod.Patch, $"/api/v1/admin/evaluation-periods/{id}/status",
            setup.Staff, new { status = open ? "Open" : "Closed" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task RegisterAsync(Fixture setup, string token) =>
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{setup.Id}/register", token, null)).StatusCode);

    private async Task<HttpResponseMessage> RecordAsync(Fixture setup)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = await db.ActivityRegistrations.Where(item => item.ActivityId == setup.Id && item.Student!.StudentCode == "SV210002")
            .Select(item => item.Id).SingleAsync();
        return await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/activities/{setup.Id}/sessions/{setup.SessionId}/check-ins",
            setup.Staff, new { registrationId = id });
    }

    private Task<HttpResponseMessage> FinalizeAsync(Fixture setup) =>
        _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/activities/{setup.Id}/scoring/finalize", setup.Staff, null);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }

    private sealed record Fixture(string Staff, string Student, int Id, int SessionId, DateTime Start, DateTime Close);
}
