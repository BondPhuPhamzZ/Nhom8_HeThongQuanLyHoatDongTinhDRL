using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Tests;

public sealed class StudentPointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public StudentPointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task PointsSummary_UsesApprovedPointsAndRecommendsEligibleOpenActivities()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync("SV210002", "123456");
        var studentId = await _api.GetCurrentUserIdAsync(student);
        var periodId = await CreatePeriodAsync(staff, "Recommendation 2032", 2032, 50);
        var activityId = await CreateActivityAsync(staff, 2032, "Cơ sở 1", "K21", 15);
        var scoreActivityId = await CreateActivityAsync(staff, 2032, "Cơ sở 1", "K21", 5);
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{scoreActivityId}/register", student, null)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registration = await context.ActivityRegistrations.SingleAsync(item => item.ActivityId == scoreActivityId);
            context.PointRecords.Add(new PointRecord
            {
                StudentId = studentId,
                ActivityId = scoreActivityId,
                RegistrationId = registration.Id,
                PeriodId = periodId,
                Points = 15,
                Status = PointRecordStatus.Pending,
                DecidedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var response = await _api.GetAsync($"/api/v1/student/reports/points-and-recommendations?periodId={periodId}", student);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal(0, data.GetProperty("approvedPoints").GetInt32());
        Assert.Equal(50, data.GetProperty("missingPoints").GetInt32());
        Assert.True(data.GetProperty("isAtRisk").GetBoolean());
        var recommendation = Assert.Single(data.GetProperty("recommendedActivities").EnumerateArray());
        Assert.Equal(activityId, recommendation.GetProperty("id").GetInt32());
        Assert.True(recommendation.GetProperty("eligibility").GetProperty("isEligible").GetBoolean());
    }

    [Fact]
    public async Task PointsSummary_HidesSuggestionsWhenTargetIsMet_AndEnforcesAccess()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync("SV210002", "123456");
        var periodId = await CreatePeriodAsync(staff, "Met target 2033", 2033, 10);
        var studentId = await _api.GetCurrentUserIdAsync(student);
        var activityId = await CreateActivityAsync(staff, 2033, "Cơ sở 1", "K21", 10);
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", student, null)).StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registration = await context.ActivityRegistrations.SingleAsync(item => item.ActivityId == activityId);
            context.PointRecords.Add(new PointRecord
            {
                StudentId = studentId,
                ActivityId = activityId,
                RegistrationId = registration.Id,
                PeriodId = periodId,
                Points = 10,
                Status = PointRecordStatus.Approved,
                DecidedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var response = await _api.GetAsync($"/api/v1/student/reports/points-and-recommendations?periodId={periodId}", student);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data");
        Assert.False(data.GetProperty("isAtRisk").GetBoolean());
        Assert.Empty(data.GetProperty("recommendedActivities").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _api.GetAsync($"/api/v1/student/reports/points-and-recommendations?periodId={periodId}", staff)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _api.GetAsync("/api/v1/student/reports/points-and-recommendations", null)).StatusCode);
    }

    [Fact]
    public async Task Recommendations_IncludeEligibleActivityBeyondFirstCatalogPage()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync("SV210002", "123456");
        var periodId = await CreatePeriodAsync(staff, $"Recommendation 2034 {Guid.NewGuid():N}", 2034, 50);
        var now = DateTime.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Activity NewCandidate(int year, DateTime createdAt) => new()
            {
                ActivityName = $"Gợi ý phân trang {Guid.NewGuid():N}",
                OrganizerName = "Phòng QL HSSV",
                Status = ActivityStatus.Published,
                CreatedAt = createdAt,
                RegistrationOpenTime = now.AddDays(-1),
                RegistrationCloseTime = now.AddDays(1),
                StartTime = new DateTime(year, 2, 1, 8, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(year, 2, 1, 10, 0, 0, DateTimeKind.Utc),
                MaxParticipants = 10,
                TrainingPoints = 5,
                Sessions = [new ActivitySession
                {
                    SessionName = "Buổi chính", Campus = "Cơ sở 1", Location = "Hội trường",
                    StartTime = new DateTime(year, 2, 1, 8, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(year, 2, 1, 10, 0, 0, DateTimeKind.Utc), SortOrder = 1
                }]
            };
            var target = NewCandidate(2034, now.AddDays(-2));
            db.Activities.Add(target);
            for (var index = 0; index < 100; index++)
                db.Activities.Add(NewCandidate(2035, now.AddDays(-1)));
            await db.SaveChangesAsync();

            var response = await _api.GetAsync($"/api/v1/student/reports/points-and-recommendations?periodId={periodId}", student);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data");
            Assert.Contains(data.GetProperty("recommendedActivities").EnumerateArray(),
                item => item.GetProperty("id").GetInt32() == target.Id);
        }
    }

    private async Task<int> CreatePeriodAsync(string staff, string name, int year, int targetPoints)
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/evaluation-periods", staff, new
        {
            name,
            startDate = $"{year}-01-01T00:00:00Z",
            endDate = $"{year}-06-30T23:59:59Z",
            targetPoints
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<int> CreateActivityAsync(string staff, int year, string campus, string academicYear, int points)
    {
        var now = DateTime.UtcNow;
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, new
        {
            activityName = $"Suggest-{Guid.NewGuid():N}",
            description = "Hoạt động kiểm thử gợi ý",
            organizerName = "Phòng CTSV",
            registrationOpenTime = Iso(now.AddDays(-1)),
            registrationCloseTime = Iso(now.AddDays(1)),
            maxParticipants = 10,
            trainingPoints = points,
            targetAudience = "Sinh viên phù hợp",
            status = "Published",
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus, location = "Hội trường", startTime = $"{year}-02-01T08:00:00Z", endTime = $"{year}-02-01T10:00:00Z", sortOrder = 1 }
            },
            audienceRules = new[] { new { campus, academicYear, classId = (int?)null, participationType = "Recommended" } }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetInt32();
    }

    private static string Iso(DateTime value) => value.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
