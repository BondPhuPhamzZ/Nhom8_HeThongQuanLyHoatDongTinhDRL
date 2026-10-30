using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Tests;

public sealed class NotificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public NotificationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task DueReminder_IsSentOnceToRegisteredOrRequiredStudents_AndOnlyOwnerCanRead()
    {
        var now = DateTime.UtcNow;
        int activityId, sessionId, registeredId, unrelatedId;
        string registeredCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registered = NewStudent("K22");
            var unrelated = NewStudent("K22");
            db.Users.AddRange(registered, unrelated);
            var activity = NewActivity(now.AddHours(2), "Published");
            activity.AudienceRules.Add(new ActivityAudienceRule { AcademicYear = "K21", ParticipationType = AudienceParticipationType.Required });
            activity.AudienceRules.Add(new ActivityAudienceRule { AcademicYear = "K22", ParticipationType = AudienceParticipationType.Optional });
            db.Activities.Add(activity);
            await db.SaveChangesAsync();
            var session = activity.Sessions.Single();
            db.ActivityRegistrations.Add(new ActivityRegistration { ActivityId = activity.Id, StudentId = registered.Id, Status = "Registered" });
            await db.SaveChangesAsync();
            activityId = activity.Id;
            sessionId = session.Id;
            registeredId = registered.Id;
            unrelatedId = unrelated.Id;
            registeredCode = registered.StudentCode;

            var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
            Assert.Equal(3, await service.DispatchDueRemindersAsync(now)); // two required K21 + one registered K22
            Assert.Equal(0, await service.DispatchDueRemindersAsync(now.AddMinutes(1)));
            Assert.Equal(3, await db.UserNotifications.CountAsync(item => item.ActivityId == activityId));
            Assert.False(await db.UserNotifications.AnyAsync(item => item.RecipientUserId == unrelatedId && item.ActivityId == activityId));
        }

        var requiredToken = await _api.LoginAsync("SV210002", "123456");
        var registeredToken = await _api.LoginAsync(registeredCode, "123456");
        var admin = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var requiredList = await DataAsync(await _api.GetAsync("/api/v1/notifications?unreadOnly=true", requiredToken));
        var notification = requiredList.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("activityId").GetInt32() == activityId);
        Assert.Equal(sessionId, notification.GetProperty("sessionId").GetInt32());
        Assert.Contains("Mang thẻ sinh viên", notification.GetProperty("body").GetString());
        var notificationId = notification.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync("/api/v1/notifications", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/notifications/{notificationId}/read", registeredToken, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/notifications/{notificationId}/read", requiredToken, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/notifications/{notificationId}/read", requiredToken, null)).StatusCode);
        var unread = await DataAsync(await _api.GetAsync("/api/v1/notifications?unreadOnly=true", requiredToken));
        Assert.DoesNotContain(unread.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == notificationId);
        Assert.True(registeredId > 0);
    }

    [Fact]
    public async Task Reminders_RespectConfiguredOffsetAndLifecycle()
    {
        var now = DateTime.UtcNow;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activity = NewActivity(now.AddHours(3), "Published");
        activity.AudienceRules.Add(new ActivityAudienceRule { AcademicYear = "K21", ParticipationType = AudienceParticipationType.Required });
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        db.ActivityReminderOffsets.Add(new ActivityReminderOffset { ActivityId = activity.Id, OffsetMinutes = 60 });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
        Assert.Equal(0, await service.DispatchDueRemindersAsync(now));
        Assert.Equal(2, await service.DispatchDueRemindersAsync(now.AddHours(2).AddSeconds(1)));
        Assert.Equal(0, await service.DispatchDueRemindersAsync(now.AddHours(2).AddMinutes(2)));

        var cancelled = NewActivity(now.AddHours(1), "Cancelled");
        cancelled.AudienceRules.Add(new ActivityAudienceRule { AcademicYear = "K21", ParticipationType = AudienceParticipationType.Required });
        db.Activities.Add(cancelled);
        await db.SaveChangesAsync();
        Assert.Equal(0, await service.DispatchDueRemindersAsync(now));
        Assert.False(await db.UserNotifications.AnyAsync(item => item.ActivityId == cancelled.Id));
    }

    private static Activity NewActivity(DateTime startUtc, string status) => new()
    {
        ActivityName = $"Nhắc lịch {Guid.NewGuid():N}",
        Requirements = "Mang thẻ sinh viên",
        OrganizerName = "Phòng QL HSSV",
        Status = status,
        StartTime = startUtc,
        EndTime = startUtc.AddHours(2),
        RegistrationOpenTime = startUtc.AddDays(-2),
        RegistrationCloseTime = startUtc.AddMinutes(-30),
        Sessions = [new ActivitySession
        {
            SessionName = "Buổi chính", Campus = "Cơ sở 1", Location = "Hội trường A",
            StartTime = startUtc, EndTime = startUtc.AddHours(2), SortOrder = 1
        }]
    };

    private static User NewStudent(string academicYear)
    {
        var code = $"SV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        return new User
        {
            StudentCode = code,
            FullName = $"Sinh viên {code}",
            Email = $"{code.ToLowerInvariant()}@school.edu.vn",
            PasswordHash = PasswordHasher.HashPassword("123456"),
            Role = Role.Student,
            Campus = "Cơ sở 1",
            AcademicYear = academicYear,
            ClassId = 2
        };
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
