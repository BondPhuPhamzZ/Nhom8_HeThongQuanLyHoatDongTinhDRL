using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Tests;

public sealed class ActivityPhaseTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public ActivityPhaseTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public void Resolve_UsesPhasePrecedenceAndTimeBoundaries()
    {
        var start = new DateTime(2040, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var session = new ActivitySession { StartTime = start, EndTime = start.AddHours(1) };
        var config = EffectiveCheckInConfig.From(1, null);
        var close = config.ClosesAt(session);
        var activity = new Activity
        {
            Status = ActivityStatus.Published, StartTime = start, EndTime = session.EndTime,
            RegistrationOpenTime = start.AddHours(-2), RegistrationCloseTime = start.AddMinutes(-5)
        };

        string Resolve(DateTime now) => ActivityPhase.Resolve(activity, [session], config, now);
        Assert.Equal(ActivityPhase.Upcoming, Resolve(activity.RegistrationOpenTime.AddTicks(-1)));
        Assert.Equal(ActivityPhase.RegistrationOpen, Resolve(activity.RegistrationOpenTime));
        Assert.Equal(ActivityPhase.RegistrationOpen, Resolve(activity.RegistrationCloseTime));
        Assert.Equal(ActivityPhase.RegistrationClosed, Resolve(activity.RegistrationCloseTime.AddTicks(1)));
        Assert.Equal(ActivityPhase.Ongoing, Resolve(start));
        Assert.Equal(ActivityPhase.Ongoing, Resolve(close.AddTicks(-1)));
        Assert.Equal(ActivityPhase.AttendanceReview, Resolve(close));
        activity.PointsFinalizedAt = close;
        Assert.Equal(ActivityPhase.Finalized, Resolve(close));
        activity.Status = ActivityStatus.Cancelled;
        Assert.Equal(ActivityPhase.Cancelled, Resolve(close));
        activity.Status = ActivityStatus.Draft;
        Assert.Equal(ActivityPhase.Draft, Resolve(close));
    }

    [Fact]
    public async Task ManualTransition_BeforeItsTime_ReturnsConflict_AndWorksAtThePhase()
    {
        var staff = await StaffAsync();
        var (id, start, close) = await CreateAsync(staff);
        var url = $"/api/v1/admin/activities/{id}/status";
        var early = await _api.SendAsync(HttpMethod.Patch, url, staff, new { status = ActivityStatus.Ongoing });
        await AccountTestClient.AssertProblemAsync(early, HttpStatusCode.Conflict, "activity_phase_not_reached");

        var activity = await DataAsync(await _api.GetAsync($"/api/v1/admin/activities/{id}", staff));
        var update = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/activities/{id}", staff,
            EditBody(activity, ActivityStatus.Completed, 3));
        await AccountTestClient.AssertProblemAsync(update, HttpStatusCode.Conflict, "activity_phase_not_reached");

        using (_factory.Time.At(start.AddSeconds(1)))
        {
            var ongoing = await _api.SendAsync(HttpMethod.Patch, url, staff, new { status = ActivityStatus.Ongoing });
            Assert.Equal(ActivityPhase.Ongoing, (await DataAsync(ongoing)).GetProperty("phase").GetString());
            var tooEarly = await _api.SendAsync(HttpMethod.Patch, url, staff, new { status = ActivityStatus.Completed });
            await AccountTestClient.AssertProblemAsync(tooEarly, HttpStatusCode.Conflict, "activity_phase_not_reached");
        }

        using (_factory.Time.At(close.AddSeconds(1)))
        {
            var completed = await _api.SendAsync(HttpMethod.Patch, url, staff, new { status = ActivityStatus.Completed });
            Assert.Equal(ActivityPhase.AttendanceReview, (await DataAsync(completed)).GetProperty("phase").GetString());
        }
    }

    [Fact]
    public async Task Worker_AdvancesOnlyPublishedOrOngoing_AndCanRunTwice()
    {
        var staff = await StaffAsync();
        var (id, start, close) = await CreateAsync(staff);
        var (draftId, _, _) = await CreateAsync(staff, ActivityStatus.Draft);
        var (cancelledId, _, _) = await CreateAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
            $"/api/v1/admin/activities/{cancelledId}/status", staff, new { status = ActivityStatus.Cancelled })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IActivityLifecycleService>();
        using (_factory.Time.At(start.AddSeconds(1)))
        {
            await lifecycle.RunOnceAsync();
            Assert.Equal(ActivityStatus.Ongoing, await StatusAsync(staff, id));
            Assert.Equal(0, await lifecycle.RunOnceAsync());
        }
        using (_factory.Time.At(close.AddSeconds(1)))
        {
            await lifecycle.RunOnceAsync();
            Assert.Equal(ActivityStatus.Completed, await StatusAsync(staff, id));
            Assert.Equal(ActivityStatus.Draft, await StatusAsync(staff, draftId));
            Assert.Equal(ActivityStatus.Cancelled, await StatusAsync(staff, cancelledId));
            Assert.Equal(0, await lifecycle.RunOnceAsync());
        }
    }

    [Fact]
    public async Task RegistrationAndAttendance_RespectPhaseEvenIfWorkerHasNotRun()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var (id, start, _) = await CreateAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/student-activities/{id}/register", student, null)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registrationId = await db.ActivityRegistrations.Where(item => item.ActivityId == id)
            .Select(item => item.Id).SingleAsync();
        var sessionId = await db.ActivitySessions.Where(item => item.ActivityId == id).Select(item => item.Id).SingleAsync();
        var earlyAttendance = await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/admin/activities/{id}/sessions/{sessionId}/check-ins", staff, new { registrationId });
        Assert.Equal(HttpStatusCode.Conflict, earlyAttendance.StatusCode);

        using (_factory.Time.At(start.AddSeconds(1)))
        {
            Assert.Equal(ActivityStatus.Published, await StatusAsync(staff, id));
            Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Post,
                $"/api/v1/student-activities/{id}/register", monitor, null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Delete,
                $"/api/v1/student-activities/{id}/registration", student, null)).StatusCode);
        }
    }

    [Fact]
    public async Task UpdateWithRegistration_ProtectsPoints_AndCancellationNotifiesEachStudentOnce()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var (id, _, close) = await CreateAsync(staff);
        foreach (var token in new[] { student, monitor })
            Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
                $"/api/v1/student-activities/{id}/register", token, null)).StatusCode);

        var url = $"/api/v1/admin/activities/{id}";
        var original = await DataAsync(await _api.GetAsync(url, staff));
        var forbidden = await _api.SendAsync(HttpMethod.Put, url, staff, EditBody(original, ActivityStatus.Published, 4));
        await AccountTestClient.AssertProblemAsync(forbidden, HttpStatusCode.Conflict, "business_rule_violation");
        var description = await _api.SendAsync(HttpMethod.Put, url, staff, EditBody(original, ActivityStatus.Published, 3));
        Assert.Equal("Mô tả mới", (await DataAsync(description)).GetProperty("description").GetString());

        using (_factory.Time.At(close.AddSeconds(1)))
            Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Patch,
                $"{url}/status", staff, new { status = ActivityStatus.Cancelled })).StatusCode);

        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
                $"{url}/status", staff, new { status = ActivityStatus.Cancelled })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var receivers = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserNotifications
            .Where(item => item.ActivityId == id && item.Kind == "ActivityCancelled")
            .Select(item => item.RecipientUserId).ToListAsync();
        Assert.Equal(2, receivers.Count);
        Assert.Equal(2, receivers.Distinct().Count());
    }

    [Fact]
    public async Task Cancellation_RevokesOnlyTheLegacyAutomaticAward_AndKeepsItsHistory()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var (id, start, _) = await CreateAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/student-activities/{id}/register", student, null)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registration = await db.ActivityRegistrations.SingleAsync(item => item.ActivityId == id);
        var period = new EvaluationPeriod
        {
            Name = $"Legacy-{Guid.NewGuid():N}", StartDate = start.AddDays(-1), EndDate = start.AddDays(2),
            Status = EvaluationPeriodStatus.Open, TargetPoints = 20, CreatedAt = start.AddHours(-1)
        };
        db.EvaluationPeriods.Add(period);
        await db.SaveChangesAsync();

        var record = new PointRecord
        {
            ActivityId = id, RegistrationId = registration.Id, StudentId = registration.StudentId,
            PeriodId = period.Id, Points = 3, Status = PointRecordStatus.Approved,
            Reason = AttendancePoints.AutoReason, DecidedAt = start,
            History =
            [
                new PointRecordHistory { ToStatus = PointRecordStatus.Approved, Points = 3,
                    Reason = AttendancePoints.AutoReason, ChangedAt = start }
            ]
        };
        db.PointRecords.Add(record);
        registration.Status = RegistrationStatus.Attended;
        registration.PointsEarned = 3;
        await db.SaveChangesAsync();

        using (_factory.Time.At(start.AddSeconds(1)))
            Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
                $"/api/v1/admin/activities/{id}/status", staff, new { status = ActivityStatus.Cancelled })).StatusCode);

        db.ChangeTracker.Clear();
        var revoked = await db.PointRecords.Include(item => item.History).SingleAsync(item => item.Id == record.Id);
        Assert.Equal(PointRecordStatus.Revoked, revoked.Status);
        Assert.Equal(2, revoked.History.Count);
        Assert.Contains(revoked.History, item => item.ToStatus == PointRecordStatus.Revoked && item.Reason == "Hoạt động bị hủy.");
        Assert.Equal(0, (await db.ActivityRegistrations.SingleAsync(item => item.Id == registration.Id)).PointsEarned);
    }

    [Fact]
    public async Task ScheduleChange_NotifiesRegisteredStudents_WhileKeepingTheSession()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var (id, _, _) = await CreateAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/student-activities/{id}/register", student, null)).StatusCode);
        var url = $"/api/v1/admin/activities/{id}";
        var activity = await DataAsync(await _api.GetAsync(url, staff));
        var session = activity.GetProperty("sessions")[0];
        var sessions = new[]
        {
            new
            {
                id = session.GetProperty("id").GetInt32(),
                sessionName = session.GetProperty("sessionName").GetString(),
                campus = session.GetProperty("campus").GetString(),
                location = "Phòng B",
                startTime = session.GetProperty("startTime").GetString(),
                endTime = session.GetProperty("endTime").GetString(),
                sortOrder = 1
            }
        };
        var updated = await DataAsync(await _api.SendAsync(HttpMethod.Put, url, staff,
            EditBody(activity, ActivityStatus.Published, 3, sessions)));
        Assert.Equal(session.GetProperty("id").GetInt32(), updated.GetProperty("sessions")[0].GetProperty("id").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var notice = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserNotifications
            .SingleAsync(item => item.ActivityId == id && item.Kind == "ActivityScheduleChanged");
        Assert.Contains("Lịch hoạt động đã thay đổi", notice.Title);
    }

    [Fact]
    public async Task LegacyOngoingActivity_CanBeEditedAndWorkerRestoresPublished()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var now = _factory.Time.GetUtcNow().UtcDateTime;
        var start = now.AddDays(1);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activity = new Activity
        {
            ActivityName = $"LegacyOngoing-{Guid.NewGuid():N}", StartTime = start, EndTime = start.AddHours(1),
            RegistrationOpenTime = now.AddHours(-1), RegistrationCloseTime = start.AddHours(-1),
            MaxParticipants = 10, TrainingPoints = 3, Status = ActivityStatus.Ongoing,
            Sessions = [new ActivitySession
            {
                SessionName = "Buổi 1", Campus = "Cơ sở 1", Location = "Hội trường",
                StartTime = start, EndTime = start.AddHours(1), SortOrder = 1
            }]
        };
        db.Activities.Add(activity);
        await db.SaveChangesAsync();

        var url = $"/api/v1/admin/activities/{activity.Id}";
        var original = await DataAsync(await _api.GetAsync(url, staff));
        var legacyEdit = await DataAsync(await _api.SendAsync(HttpMethod.Put, url, staff,
            EditBody(original, ActivityStatus.Ongoing, 3)));
        Assert.Equal(ActivityStatus.Ongoing, legacyEdit.GetProperty("status").GetString());

        var lifecycle = scope.ServiceProvider.GetRequiredService<IActivityLifecycleService>();
        await lifecycle.RunOnceAsync();
        Assert.Equal(ActivityStatus.Published, await StatusAsync(staff, activity.Id));
        Assert.Equal(0, await lifecycle.RunOnceAsync());
        var repairedEdit = await _api.SendAsync(HttpMethod.Put, url, staff, EditBody(original, ActivityStatus.Published, 3));
        Assert.Equal(HttpStatusCode.OK, repairedEdit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/student-activities/{activity.Id}/register", student, null)).StatusCode);
    }

    [Fact]
    public async Task CancelledActivity_RemainsVisibleOnlyToRegisteredStudent()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var (id, _, _) = await CreateAsync(staff);
        var url = $"/api/v1/student-activities/{id}";
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"{url}/register", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch,
            $"/api/v1/admin/activities/{id}/status", staff, new { status = ActivityStatus.Cancelled })).StatusCode);
        var registeredView = await DataAsync(await _api.GetAsync(url, student));
        Assert.Equal(ActivityPhase.Cancelled, registeredView.GetProperty("phase").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(url, monitor)).StatusCode);
    }

    [Fact]
    public async Task OngoingActivity_RejectsChangingItsNameAfterRegistration()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var (id, start, _) = await CreateAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/student-activities/{id}/register", student, null)).StatusCode);
        using var clock = _factory.Time.At(start.AddSeconds(1));
        var url = $"/api/v1/admin/activities/{id}";
        var activity = await DataAsync(await _api.GetAsync(url, staff));
        var update = await _api.SendAsync(HttpMethod.Put, url, staff,
            EditBody(activity, ActivityStatus.Published, 3, activityName: "Tên đã bị đổi"));
        await AccountTestClient.AssertProblemAsync(update, HttpStatusCode.Conflict, "business_rule_violation");
    }

    [Fact]
    public async Task StudentList_ShowsEndedSessionUntilCheckInCloses_EvenBeforeWorkerRuns()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var (id, _, close) = await CreateAsync(staff);
        using (_factory.Time.At(close.AddMinutes(-10)))
        {
            Assert.Equal(ActivityStatus.Published, await StatusAsync(staff, id));
            var visible = await DataAsync(await _api.GetAsync("/api/v1/student-activities?pageSize=100", student));
            Assert.Contains(visible.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);
        }
        using (_factory.Time.At(close.AddSeconds(1)))
        {
            var hidden = await DataAsync(await _api.GetAsync("/api/v1/student-activities?pageSize=100", student));
            Assert.DoesNotContain(hidden.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);
        }
    }

    [Fact]
    public async Task LatePublishedActivities_CanCatchUpDirectlyToCompleted()
    {
        var staff = await StaffAsync();
        var (workerId, _, _) = await CreateAsync(staff);
        var (patchId, _, close) = await CreateAsync(staff);
        using var clock = _factory.Time.At(close.AddSeconds(1));
        Assert.Equal(ActivityStatus.Published, await StatusAsync(staff, workerId));
        Assert.Equal(ActivityStatus.Published, await StatusAsync(staff, patchId));

        var patched = await DataAsync(await _api.SendAsync(HttpMethod.Patch,
            $"/api/v1/admin/activities/{patchId}/status", staff, new { status = ActivityStatus.Completed }));
        Assert.Equal(ActivityStatus.Completed, patched.GetProperty("status").GetString());
        Assert.Equal(ActivityPhase.AttendanceReview, patched.GetProperty("phase").GetString());

        using var scope = _factory.Services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IActivityLifecycleService>();
        await lifecycle.RunOnceAsync();
        Assert.Equal(ActivityStatus.Completed, await StatusAsync(staff, workerId));
        Assert.Equal(0, await lifecycle.RunOnceAsync());
    }

    private Task<string> StaffAsync() => _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    private async Task<(int Id, DateTime Start, DateTime Close)> CreateAsync(string staff, string status = ActivityStatus.Published)
    {
        var now = _factory.Time.GetUtcNow();
        var start = now.AddHours(1);
        var body = new
        {
            activityName = $"Phase-{Guid.NewGuid():N}", organizerName = "Phòng CTSV",
            registrationOpenTime = now.AddMinutes(-5), registrationCloseTime = start.AddMinutes(-2),
            maxParticipants = 10, trainingPoints = 3, status,
            sessions = new[]
            {
                new { sessionName = "Buổi 1", campus = "Cơ sở 1", location = "Hội trường",
                    startTime = start, endTime = start.AddMinutes(10), sortOrder = 1 }
            }
        };
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", staff, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await DataAsync(response);
        return (data.GetProperty("id").GetInt32(), data.GetProperty("startTime").GetDateTime(),
            data.GetProperty("checkInClosesAt").GetDateTime());
    }

    private async Task<string?> StatusAsync(string staff, int id) =>
        (await DataAsync(await _api.GetAsync($"/api/v1/admin/activities/{id}", staff))).GetProperty("status").GetString();

    private static object EditBody(JsonElement activity, string status, int trainingPoints, object? sessions = null, string? activityName = null) => new
    {
        activityName = activityName ?? activity.GetProperty("activityName").GetString(),
        organizerName = activity.GetProperty("organizerName").GetString(),
        description = "Mô tả mới",
        registrationOpenTime = activity.GetProperty("registrationOpenTime").GetString(),
        registrationCloseTime = activity.GetProperty("registrationCloseTime").GetString(),
        sessions = sessions ?? activity.GetProperty("sessions"),
        maxParticipants = 10,
        trainingPoints,
        status
    };

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }
}
