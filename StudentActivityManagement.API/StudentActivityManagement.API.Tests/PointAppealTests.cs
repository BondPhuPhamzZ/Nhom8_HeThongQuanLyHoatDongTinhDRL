using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Tests;

public sealed class PointAppealTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public PointAppealTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task StudentCanAppealRejectedRecordOnceAndWithdrawThenAppealAgain()
    {
        var setup = await SetupAsync();
        var created = await CreateAsync(setup);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appealId = (await DataAsync(created)).GetProperty("id").GetInt32();
        await AccountTestClient.AssertProblemAsync(await CreateAsync(setup), HttpStatusCode.Conflict, "business_rule_violation");
        var mine = (await AccountTestClient.ReadJsonAsync(await _api.GetAsync("/api/v1/student/point-appeals", setup.Student)))
            .GetProperty("data");
        Assert.Contains(mine.EnumerateArray(), item => item.GetProperty("id").GetInt32() == appealId);
        var withdrawn = await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/student/point-appeals/{appealId}/withdraw", setup.Student, null);
        Assert.Equal("Withdrawn", (await DataAsync(withdrawn)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(setup)).StatusCode);
    }

    [Fact]
    public async Task CreateChecksOwnershipDecisionWindowAndPeriod()
    {
        var setup = await SetupAsync();
        var other = await _api.LoginAsync("SV210001", "123456");
        await AccountTestClient.AssertProblemAsync(
            await _api.SendAsync(HttpMethod.Post, "/api/v1/student/point-appeals", other,
                new { pointRecordId = setup.RecordId, reason = "Tôi muốn kiểm tra lại điểm này" }),
            HttpStatusCode.NotFound, "resource_not_found");
        await SetRecordAsync(setup.RecordId, status: PointRecordStatus.Approved);
        await AccountTestClient.AssertProblemAsync(await CreateAsync(setup), HttpStatusCode.Conflict, "business_rule_violation");
        await SetRecordAsync(setup.RecordId, status: PointRecordStatus.Pending);
        await AccountTestClient.AssertProblemAsync(await CreateAsync(setup), HttpStatusCode.Conflict, "business_rule_violation");
        await SetRecordAsync(setup.RecordId, status: PointRecordStatus.Rejected, decidedAt: DateTime.UtcNow.AddDays(-15));
        await AccountTestClient.AssertProblemAsync(await CreateAsync(setup), HttpStatusCode.Conflict, "business_rule_violation");
        await SetRecordAsync(setup.RecordId, decidedAt: DateTime.UtcNow);
        await SetPeriodStatusAsync(setup.PeriodId, EvaluationPeriodStatus.Closed);
        await AccountTestClient.AssertProblemAsync(await CreateAsync(setup), HttpStatusCode.Conflict, "business_rule_violation");
    }

    [Fact]
    public async Task AcceptTransitionsTwiceAddsPointsHistoryAndNotification()
    {
        var setup = await SetupAsync();
        var appealId = (await DataAsync(await CreateAsync(setup))).GetProperty("id").GetInt32();
        var before = await PointsAsync(setup);
        var result = await ResolveAsync(setup, appealId, true);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("Accepted", (await DataAsync(result)).GetProperty("status").GetString());
        Assert.Equal(before + 7, await PointsAsync(setup));
        var history = (await AccountTestClient.ReadJsonAsync(await _api.GetAsync(
            $"/api/v1/admin/point-records/{setup.RecordId}/history", setup.Staff))).GetProperty("data");
        Assert.Equal(new[] { "Pending", "Approved" }, history.EnumerateArray()
            .Select(item => item.GetProperty("toStatus").GetString()).ToArray());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PointRecordStatus.Approved, (await db.PointRecords.AsNoTracking()
            .SingleAsync(item => item.Id == setup.RecordId)).Status);
        Assert.Single(await db.UserNotifications.Where(item => item.RecipientUserId == setup.StudentId
            && item.Kind == "PointAppeal" && item.SourceKey == $"point-appeal:{appealId}:resolved").ToListAsync());
        var activity = (await AccountTestClient.ReadJsonAsync(await _api.GetAsync(
            $"/api/v1/student/reports/activity-history?periodId={setup.PeriodId}", setup.Student)))
            .GetProperty("data").GetProperty("activities");
        Assert.Equal("Accepted", activity.EnumerateArray()
            .Single(item => item.GetProperty("pointRecordId").ValueKind != System.Text.Json.JsonValueKind.Null
                && item.GetProperty("pointRecordId").GetInt32() == setup.RecordId)
            .GetProperty("appealStatus").GetString());
    }

    [Fact]
    public async Task RejectLeavesRecordRejectedAndSendsNotification()
    {
        var setup = await SetupAsync();
        var appealId = (await DataAsync(await CreateAsync(setup))).GetProperty("id").GetInt32();
        var result = await ResolveAsync(setup, appealId, false);
        Assert.Equal("Rejected", (await DataAsync(result)).GetProperty("status").GetString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PointRecordStatus.Rejected, (await db.PointRecords.AsNoTracking().SingleAsync(item => item.Id == setup.RecordId)).Status);
        Assert.Single(await db.UserNotifications.Where(item => item.Kind == "PointAppeal" && item.RecipientUserId == setup.StudentId).ToListAsync());
    }

    [Fact]
    public async Task LockedPeriodBlocksAcceptAndKeepsAppealPending()
    {
        var setup = await SetupAsync();
        var appealId = (await DataAsync(await CreateAsync(setup))).GetProperty("id").GetInt32();
        await SetPeriodStatusAsync(setup.PeriodId, EvaluationPeriodStatus.Closed);
        await AccountTestClient.AssertProblemAsync(await ResolveAsync(setup, appealId, true),
            HttpStatusCode.Conflict, "business_rule_violation");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PointAppealStatus.Pending, (await db.PointAppeals.AsNoTracking().SingleAsync(item => item.Id == appealId)).Status);
        Assert.Equal(PointRecordStatus.Rejected, (await db.PointRecords.AsNoTracking().SingleAsync(item => item.Id == setup.RecordId)).Status);
    }

    [Fact]
    public async Task EndpointsEnforceBothPoliciesAndInboxFilters()
    {
        var setup = await SetupAsync();
        var appealId = (await DataAsync(await CreateAsync(setup))).GetProperty("id").GetInt32();
        await AccountTestClient.AssertProblemAsync(await _api.GetAsync("/api/v1/admin/point-appeals", setup.Student),
            HttpStatusCode.Forbidden, "access_denied");
        await AccountTestClient.AssertProblemAsync(await _api.GetAsync("/api/v1/student/point-appeals", setup.Staff),
            HttpStatusCode.Forbidden, "access_denied");
        var inbox = (await AccountTestClient.ReadJsonAsync(await _api.GetAsync(
            $"/api/v1/admin/point-appeals?status=Pending&periodId={setup.PeriodId}", setup.Staff))).GetProperty("data");
        Assert.Equal(appealId, Assert.Single(inbox.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(1, inbox.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ReasonAndStaffResponseMustRemainLongEnoughAfterTrimming()
    {
        var setup = await SetupAsync();
        await AccountTestClient.AssertProblemAsync(await _api.SendAsync(HttpMethod.Post,
            "/api/v1/student/point-appeals", setup.Student,
            new { pointRecordId = setup.RecordId, reason = "          a          " }),
            HttpStatusCode.BadRequest, "validation_failed");
        var appealId = (await DataAsync(await CreateAsync(setup))).GetProperty("id").GetInt32();
        await AccountTestClient.AssertProblemAsync(await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/point-appeals/{appealId}/resolve", setup.Staff,
            new { accept = true, response = "          a          " }),
            HttpStatusCode.BadRequest, "validation_failed");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(PointAppealStatus.Pending, (await db.PointAppeals.AsNoTracking()
            .SingleAsync(item => item.Id == appealId)).Status);
    }

    private sealed record Setup(string Staff, string Student, int StudentId, int RecordId, int PeriodId);

    private async Task<Setup> SetupAsync()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var student = await _api.LoginAsync("SV210002", "123456");
        var studentId = await _api.GetCurrentUserIdAsync(student);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var period = new EvaluationPeriod { Name = $"Appeal-{Guid.NewGuid():N}", StartDate = now.AddDays(-2),
            EndDate = now.AddDays(2), TargetPoints = 20, Status = EvaluationPeriodStatus.Open, CreatedAt = now };
        var activity = new Activity { ActivityName = $"Hoạt động {Guid.NewGuid():N}", OrganizerName = "Phòng QL HSSV",
            StartTime = now.AddDays(-1), EndTime = now.AddHours(-1), Status = ActivityStatus.Completed,
            TrainingPoints = 7, RegistrationOpenTime = now.AddDays(-3), RegistrationCloseTime = now.AddDays(-2) };
        db.EvaluationPeriods.Add(period);
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        db.ActivitySessions.Add(new ActivitySession { ActivityId = activity.Id, SessionName = "Buổi kiểm thử",
            Campus = "Cơ sở 1", Location = "Hội trường", StartTime = activity.StartTime, EndTime = activity.EndTime, SortOrder = 1 });
        var registration = new ActivityRegistration { ActivityId = activity.Id, StudentId = studentId,
            Status = RegistrationStatus.Attended, RegisteredAt = now.AddDays(-2) };
        db.ActivityRegistrations.Add(registration);
        await db.SaveChangesAsync();
        var record = new PointRecord { ActivityId = activity.Id, RegistrationId = registration.Id,
            StudentId = studentId, PeriodId = period.Id, Points = 7, Status = PointRecordStatus.Rejected,
            Reason = "Chưa đủ minh chứng", DecidedAt = now, DecidedById = await db.Users
                .Where(item => item.StudentCode == AccountTestClient.HssvCode).Select(item => item.Id).SingleAsync() };
        db.PointRecords.Add(record);
        await db.SaveChangesAsync();
        return new Setup(staff, student, studentId, record.Id, period.Id);
    }

    private Task<HttpResponseMessage> CreateAsync(Setup setup) => _api.SendAsync(HttpMethod.Post,
        "/api/v1/student/point-appeals", setup.Student,
        new { pointRecordId = setup.RecordId, reason = "Đề nghị kiểm tra lại quyết định điểm này." });
    private Task<HttpResponseMessage> ResolveAsync(Setup setup, int id, bool accept) => _api.SendAsync(HttpMethod.Post,
        $"/api/v1/admin/point-appeals/{id}/resolve", setup.Staff,
        new { accept, response = "Đã kiểm tra hồ sơ và phản hồi khiếu nại." });
    private static async Task<System.Text.Json.JsonElement> DataAsync(HttpResponseMessage response) =>
        (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data");

    private async Task<int> PointsAsync(Setup setup) =>
        (await AccountTestClient.ReadJsonAsync(await _api.GetAsync(
            $"/api/v1/student/reports/points-and-recommendations?periodId={setup.PeriodId}", setup.Student)))
            .GetProperty("data").GetProperty("approvedPoints").GetInt32();

    private async Task SetRecordAsync(int id, string? status = null, DateTime? decidedAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.PointRecords.SingleAsync(item => item.Id == id);
        if (status != null) record.Status = status;
        if (decidedAt.HasValue) record.DecidedAt = decidedAt.Value;
        await db.SaveChangesAsync();
    }

    private async Task SetPeriodStatusAsync(int id, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var period = await db.EvaluationPeriods.SingleAsync(item => item.Id == id);
        period.Status = status;
        await db.SaveChangesAsync();
    }
}
