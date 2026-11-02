using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Tests;

public sealed class StudentReportTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public StudentReportTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task StudentHistory_IsScopedToPeriodAndShowsOnlyApprovedPoints()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync("SV210002", "123456");
        var studentId = await _api.GetCurrentUserIdAsync(student);
        var periodResponse = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/evaluation-periods", staff, new
        {
            name = "History 2031",
            startDate = "2031-01-01T00:00:00Z",
            endDate = "2031-06-30T23:59:59Z",
            targetPoints = 50
        });
        Assert.Equal(HttpStatusCode.Created, periodResponse.StatusCode);
        var period = await AccountTestClient.ReadJsonAsync(periodResponse);
        var periodId = period.GetProperty("data").GetProperty("id").GetInt32();
        var activityId = await CreateActivityAsync(staff, student, "2031-02-01T08:00:00Z", "2031-02-01T10:00:00Z");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registration = await context.ActivityRegistrations.SingleAsync(item => item.ActivityId == activityId);
            var session = await context.ActivitySessions.SingleAsync(item => item.ActivityId == activityId);
            context.ActivityCheckIns.Add(new ActivityCheckIn
            {
                RegistrationId = registration.Id,
                SessionId = session.Id,
                CheckedInAt = DateTime.UtcNow,
                Method = CheckInMethods.Code
            });
            context.PointRecords.Add(new PointRecord
            {
                RegistrationId = registration.Id,
                StudentId = studentId,
                ActivityId = activityId,
                PeriodId = periodId,
                Points = 5,
                Status = PointRecordStatus.Approved,
                DecidedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var periodsResponse = await _api.GetAsync("/api/v1/student/reports/evaluation-periods", student);
        Assert.Equal(HttpStatusCode.OK, periodsResponse.StatusCode);
        var periods = (await AccountTestClient.ReadJsonAsync(periodsResponse)).GetProperty("data");
        Assert.Contains(periods.EnumerateArray(), item => item.GetProperty("id").GetInt32() == periodId && item.GetProperty("approvedPoints").GetInt32() == 5);

        var historyResponse = await _api.GetAsync(
            $"/api/v1/student/reports/activity-history?periodId={periodId}&registrationStatus=registered", student);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = (await AccountTestClient.ReadJsonAsync(historyResponse)).GetProperty("data");
        Assert.Equal(5, history.GetProperty("period").GetProperty("approvedPoints").GetInt32());
        var activity = Assert.Single(history.GetProperty("activities").EnumerateArray());
        Assert.Equal(activityId, activity.GetProperty("activityId").GetInt32());
        Assert.Equal(1, activity.GetProperty("checkInCount").GetInt32());
        Assert.Equal("Approved", activity.GetProperty("pointStatus").GetString());
        Assert.Equal(5, activity.GetProperty("pointsAwarded").GetInt32());
    }

    [Fact]
    public async Task StudentHistory_EnforcesRoleAndQueryValidation()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync("SV210002", "123456");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await _api.GetAsync("/api/v1/student/reports/evaluation-periods", staff)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _api.GetAsync("/api/v1/student/reports/evaluation-periods", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _api.GetAsync("/api/v1/student/reports/activity-history?registrationStatus=Unknown", student)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _api.GetAsync("/api/v1/student/reports/activity-history?periodId=2147483000", student)).StatusCode);
    }

    private async Task<int> CreateActivityAsync(string staff, string student, string start, string end)
    {
        var now = DateTime.UtcNow;
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"History-{Guid.NewGuid():N}",
            description = "Hoạt động kiểm thử lịch sử",
            organizerName = "Phòng CTSV",
            registrationOpenTime = Iso(now.AddHours(-1)),
            registrationCloseTime = Iso(now.AddHours(1)),
            maxParticipants = 10,
            trainingPoints = 5,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường", startTime = start, endTime = end, sortOrder = 1 }
            },
            audienceRules = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activityId = (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", student, null)).StatusCode);
        return activityId;
    }

    private static string Iso(DateTime value) => value.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
