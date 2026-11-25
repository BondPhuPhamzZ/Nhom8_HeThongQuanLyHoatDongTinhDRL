using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;

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
    public async Task HistoryReportsCanAppealWithTheSameRulesAsTheAppealEndpoint()
    {
        var setup = await SetupAsync();
        async Task<bool> CanAppealAsync() => (await AccountTestClient.ReadJsonAsync(await _api.GetAsync(
                $"/api/v1/student/reports/activity-history?periodId={setup.PeriodId}", setup.Student)))
            .GetProperty("data").GetProperty("activities").EnumerateArray()
            .Single(item => item.GetProperty("pointRecordId").ValueKind == System.Text.Json.JsonValueKind.Number
                && item.GetProperty("pointRecordId").GetInt32() == setup.RecordId)
            .GetProperty("canAppeal").GetBoolean();

        Assert.True(await CanAppealAsync());
        var appealId = (await DataAsync(await CreateAsync(setup))).GetProperty("id").GetInt32();
        Assert.False(await CanAppealAsync()); // an appeal is pending
        await _api.SendAsync(HttpMethod.Post, $"/api/v1/student/point-appeals/{appealId}/withdraw", setup.Student, null);
        Assert.True(await CanAppealAsync());
        await SetRecordAsync(setup.RecordId, decidedAt: DateTime.UtcNow.AddDays(-15));
        Assert.False(await CanAppealAsync()); // outside PointAppeals:WindowDays
        await SetRecordAsync(setup.RecordId, decidedAt: DateTime.UtcNow);
        await SetPeriodStatusAsync(setup.PeriodId, EvaluationPeriodStatus.Closed);
        Assert.False(await CanAppealAsync()); // period locked
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

    [Fact]
    public async Task Accept_FinalizedAbsence_RestoresParticipationAndPointsWithoutChangingCheckInLog()
    {
        var setup = await SetupAsync(finalizeAbsence: true);
        var historyBefore = await ActivityHistoryAsync(setup);
        Assert.True(historyBefore.GetProperty("canAppeal").GetBoolean());
        Assert.True(historyBefore.GetProperty("isAbsence").GetBoolean());
        Assert.Equal(RegistrationStatus.Absent, historyBefore.GetProperty("registrationStatus").GetString());
        var activityId = historyBefore.GetProperty("activityId").GetInt32();
        var registrationId = historyBefore.GetProperty("registrationId").GetInt32();
        var logBefore = await LogAsync();
        var pointsBefore = await PointsAsync(setup);

        var created = await CreateAsync(setup);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appeal = await DataAsync(created);
        var appealId = appeal.GetProperty("id").GetInt32();
        Assert.Equal(registrationId, appeal.GetProperty("registrationId").GetInt32());
        Assert.Equal(AttendancePoints.AbsentReason, appeal.GetProperty("recordReason").GetString());
        Assert.True(appeal.GetProperty("isAbsence").GetBoolean());
        var inbox = await DataAsync(await _api.GetAsync(
            $"/api/v1/admin/point-appeals?status=Pending&periodId={setup.PeriodId}", setup.Staff));
        Assert.True(Assert.Single(inbox.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetInt32() == appealId).GetProperty("isAbsence").GetBoolean());

        var resolved = await ResolveAsync(setup, appealId, true);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(PointAppealStatus.Accepted, (await DataAsync(resolved)).GetProperty("status").GetString());
        var historyAfter = await ActivityHistoryAsync(setup);
        Assert.Equal(PointRecordStatus.Approved, historyAfter.GetProperty("pointStatus").GetString());
        Assert.Equal(RegistrationStatus.Attended, historyAfter.GetProperty("registrationStatus").GetString());
        Assert.Equal(pointsBefore + historyBefore.GetProperty("trainingPoints").GetInt32(), await PointsAsync(setup));
        Assert.Equal(logBefore, await LogAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.UserNotifications.Where(item => item.RecipientUserId == setup.StudentId
            && item.SourceKey == $"point-appeal:{appealId}:resolved").ToListAsync());

        async Task<string> LogAsync() => (await DataAsync(await _api.GetAsync(
            $"/api/v1/admin/activities/{activityId}/check-in-log?registrationId={registrationId}", setup.Staff))).GetRawText();
    }

    [Fact]
    public async Task Accept_RejectedAbsenceWithChangedReason_RestoresParticipationAndPoints()
    {
        var setup = await SetupAsync(finalizeAbsence: true);
        var pointsBefore = await PointsAsync(setup);
        const string newReason = "Chưa có căn cứ xác nhận sinh viên đã tham gia.";
        foreach (var status in new[] { PointRecordStatus.Pending, PointRecordStatus.Rejected })
        {
            var transition = await _api.SendAsync(HttpMethod.Post,
                $"/api/v1/admin/point-records/{setup.RecordId}/transition", setup.Staff,
                new { status, reason = newReason });
            Assert.Equal(HttpStatusCode.OK, transition.StatusCode);
        }

        var historyBefore = await ActivityHistoryAsync(setup);
        Assert.True(historyBefore.GetProperty("canAppeal").GetBoolean());
        Assert.True(historyBefore.GetProperty("isAbsence").GetBoolean());
        Assert.Equal(newReason, historyBefore.GetProperty("pointReason").GetString());

        var created = await CreateAsync(setup);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appeal = await DataAsync(created);
        Assert.Equal(newReason, appeal.GetProperty("recordReason").GetString());
        Assert.True(appeal.GetProperty("isAbsence").GetBoolean());
        var mine = await DataAsync(await _api.GetAsync("/api/v1/student/point-appeals", setup.Student));
        Assert.Contains(mine.EnumerateArray(), item => item.GetProperty("id").GetInt32() == appeal.GetProperty("id").GetInt32()
            && item.GetProperty("isAbsence").GetBoolean());

        var resolved = await ResolveAsync(setup, appeal.GetProperty("id").GetInt32(), true);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.False((await DataAsync(resolved)).GetProperty("isAbsence").GetBoolean());
        var historyAfter = await ActivityHistoryAsync(setup);
        Assert.Equal(RegistrationStatus.Attended, historyAfter.GetProperty("registrationStatus").GetString());
        Assert.Equal(PointRecordStatus.Approved, historyAfter.GetProperty("pointStatus").GetString());
        Assert.Equal(pointsBefore + historyBefore.GetProperty("trainingPoints").GetInt32(), await PointsAsync(setup));
    }

    [Fact]
    public async Task Create_AfterAbsenceAppealWindowOrWhenRevoked_ReturnsConflict()
    {
        var setup = await SetupAsync(finalizeAbsence: true);
        var decidedAt = (await ActivityHistoryAsync(setup)).GetProperty("pointDecidedAt").GetDateTime();
        using (_factory.Time.At(decidedAt.AddDays(14).AddSeconds(-1)))
            Assert.True((await ActivityHistoryAsync(setup)).GetProperty("canAppeal").GetBoolean());
        using (_factory.Time.At(decidedAt.AddDays(14).AddSeconds(1)))
        {
            Assert.False((await ActivityHistoryAsync(setup)).GetProperty("canAppeal").GetBoolean());
            await AccountTestClient.AssertProblemAsync(await CreateAsync(setup), HttpStatusCode.Conflict, "business_rule_violation");
        }

        await SetRecordAsync(setup.RecordId, status: PointRecordStatus.Revoked);
        Assert.False((await ActivityHistoryAsync(setup)).GetProperty("canAppeal").GetBoolean());
        await AccountTestClient.AssertProblemAsync(await CreateAsync(setup), HttpStatusCode.Conflict, "business_rule_violation");
    }

    [Fact]
    public async Task Create_OrdinaryRejection_ReturnsDecisionReasonWithoutAbsenceFlag()
    {
        var setup = await SetupAsync();
        var created = await CreateAsync(setup);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appeal = await DataAsync(created);
        Assert.False(appeal.GetProperty("isAbsence").GetBoolean());
        Assert.Equal("Chưa đủ minh chứng", appeal.GetProperty("recordReason").GetString());
    }

    private async Task<System.Text.Json.JsonElement> ActivityHistoryAsync(Setup setup)
    {
        var history = await DataAsync(await _api.GetAsync(
            $"/api/v1/student/reports/activity-history?periodId={setup.PeriodId}", setup.Student));
        return history.GetProperty("activities").EnumerateArray()
            .Single(item => item.GetProperty("pointRecordId").ValueKind == System.Text.Json.JsonValueKind.Number
                && item.GetProperty("pointRecordId").GetInt32() == setup.RecordId);
    }

    private sealed record Setup(string Staff, string Student, int StudentId, int RecordId, int PeriodId);

    private async Task<Setup> SetupAsync(bool finalizeAbsence = false)
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
            Status = finalizeAbsence ? RegistrationStatus.Registered : RegistrationStatus.Attended,
            RegisteredAt = now.AddDays(-2) };
        db.ActivityRegistrations.Add(registration);
        await db.SaveChangesAsync();
        if (finalizeAbsence)
        {
            // Use the period selected by the real finalizer, including an existing open period.
            var finalized = await _api.SendAsync(HttpMethod.Post,
                $"/api/v1/admin/activities/{activity.Id}/scoring/finalize", staff, null);
            Assert.True(finalized.IsSuccessStatusCode, await finalized.Content.ReadAsStringAsync());
            var absence = await db.PointRecords.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.Id);
            return new Setup(staff, student, studentId, absence.Id, absence.PeriodId);
        }
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
