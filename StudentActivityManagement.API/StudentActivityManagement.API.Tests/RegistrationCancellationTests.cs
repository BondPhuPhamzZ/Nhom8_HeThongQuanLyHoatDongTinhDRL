using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;

namespace StudentActivityManagement.API.Tests;

public sealed class RegistrationCancellationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RegistrationCancellationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CancelRegistration_Success_DecrementsCountAndPreservesHistory()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityName = $"Cancel-Success-{Guid.NewGuid():N}";
        var activityId = await CreateActivityAsync(activityName);

        // Student registers
        await AuthorizeAsync("SV210002", "123456");
        var registerResponse = await _client.PostAsync($"/api/v1/student-activities/{activityId}/register", null);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Verify count is 1
        var detailBefore = await GetActivityDetailAsync(activityId);
        Assert.Equal(1, detailBefore.GetProperty("currentParticipantsCount").GetInt32());

        // Cancel registration
        var cancelResponse = await _client.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // Verify count decremented to 0
        var detailAfter = await GetActivityDetailAsync(activityId);
        Assert.Equal(0, detailAfter.GetProperty("currentParticipantsCount").GetInt32());

        // Verify registration record in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registration = await db.ActivityRegistrations
            .FirstOrDefaultAsync(r => r.ActivityId == activityId && r.Student!.StudentCode == "SV210002");
        Assert.NotNull(registration);
        Assert.Equal("Cancelled", registration.Status);
        Assert.True(registration.RegisteredAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CancelRegistration_Repeated_IsIdempotentAndDoesNotDecrementTwice()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityName = $"Cancel-Idempotent-{Guid.NewGuid():N}";
        var activityId = await CreateActivityAsync(activityName);

        // Student registers then cancels
        await AuthorizeAsync("SV210002", "123456");
        var registerResponse = await _client.PostAsync($"/api/v1/student-activities/{activityId}/register", null);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var firstCancel = await _client.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        Assert.Equal(HttpStatusCode.OK, firstCancel.StatusCode);

        // Repeated cancellation call
        var secondCancel = await _client.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        Assert.Equal(HttpStatusCode.OK, secondCancel.StatusCode);

        // Verify count is still 0 (not negative)
        var detail = await GetActivityDetailAsync(activityId);
        Assert.Equal(0, detail.GetProperty("currentParticipantsCount").GetInt32());
    }

    [Fact]
    public async Task CancelRegistration_InconsistentCapacity_RollsBackStatusChange()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityId = await CreateActivityAsync($"Cancel-Inconsistent-{Guid.NewGuid():N}");

        await AuthorizeAsync("SV210002", "123456");
        var registerResponse = await _client.PostAsync($"/api/v1/student-activities/{activityId}/register", null);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Activities.Where(activity => activity.Id == activityId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(activity => activity.CurrentParticipantsCount, 0));
        }

        var cancelResponse = await _client.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registration = await verifyDb.ActivityRegistrations.AsNoTracking()
            .FirstAsync(item => item.ActivityId == activityId);
        Assert.Equal("Registered", registration.Status);
        Assert.Equal(0, await verifyDb.Activities.Where(item => item.Id == activityId)
            .Select(item => item.CurrentParticipantsCount).SingleAsync());
    }

    [Fact]
    public async Task CancelRegistration_MissingRegistration_Returns404ProblemDetails()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityName = $"Cancel-Missing-{Guid.NewGuid():N}";
        var activityId = await CreateActivityAsync(activityName);

        // Student tries to cancel without ever registering
        await AuthorizeAsync("SV210002", "123456");
        var response = await _client.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("resource_not_found", await ReadStringAsync(response, "errorCode"));

        // Non-existent activity
        var notFoundActivity = await _client.DeleteAsync("/api/v1/student-activities/999999/registration");
        Assert.Equal(HttpStatusCode.NotFound, notFoundActivity.StatusCode);
        Assert.Equal("application/problem+json", notFoundActivity.Content.Headers.ContentType?.MediaType);
        Assert.Equal("resource_not_found", await ReadStringAsync(notFoundActivity, "errorCode"));
    }

    [Fact]
    public async Task CancelRegistration_PastRegistrationCloseTime_Returns409ProblemDetails()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityName = $"Cancel-PastClose-{Guid.NewGuid():N}";
        var activityId = await CreateActivityAsync(activityName);

        // Student registers while open
        await AuthorizeAsync("SV210002", "123456");
        var regResponse = await _client.PostAsync($"/api/v1/student-activities/{activityId}/register", null);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // Advance registration close time into the past
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activity = await db.Activities.FindAsync(activityId);
            Assert.NotNull(activity);
            activity.RegistrationCloseTime = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        // Student attempts to cancel after close time
        var cancelResponse = await _client.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
        Assert.Equal("application/problem+json", cancelResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("resource_conflict", await ReadStringAsync(cancelResponse, "errorCode"));
    }

    [Fact]
    public async Task CancelRegistration_UnauthorizedAndWrongRole_ReturnsExpectedErrors()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityName = $"Cancel-AuthGuard-{Guid.NewGuid():N}";
        var activityId = await CreateActivityAsync(activityName);

        // 1. Missing token -> 401
        _client.DefaultRequestHeaders.Authorization = null;
        var unauthResponse = await _client.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);
        Assert.Equal("application/problem+json", unauthResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("authentication_required", await ReadStringAsync(unauthResponse, "errorCode"));

        // 2. Admin token (Wrong role for StudentOrMonitor policy) -> 403
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var forbiddenResponse = await _client.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal("application/problem+json", forbiddenResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("access_denied", await ReadStringAsync(forbiddenResponse, "errorCode"));
    }

    [Fact]
    public async Task CancelRegistration_ConcurrentRequests_OnlyDecrementsOnce()
    {
        await AuthorizeAsync("ADMIN001", "Admin123@");
        var activityName = $"Cancel-Concurrent-{Guid.NewGuid():N}";
        var activityId = await CreateActivityAsync(activityName);

        // Student registers
        await AuthorizeAsync("SV210002", "123456");
        var regResponse = await _client.PostAsync($"/api/v1/student-activities/{activityId}/register", null);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        var token = await LoginAsync("SV210002", "123456");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Send two concurrent DELETE requests
        var task1 = client1.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        var task2 = client2.DeleteAsync($"/api/v1/student-activities/{activityId}/registration");
        var responses = await Task.WhenAll(task1, task2);

        // Both return 200 (one is actual cancellation, one is idempotent duplicate)
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Capacity must decrement exactly once to 0
        var detail = await GetActivityDetailAsync(activityId);
        Assert.Equal(0, detail.GetProperty("currentParticipantsCount").GetInt32());

        // Status must be Cancelled
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registration = await db.ActivityRegistrations
            .FirstOrDefaultAsync(r => r.ActivityId == activityId && r.Student!.StudentCode == "SV210002");
        Assert.NotNull(registration);
        Assert.Equal("Cancelled", registration.Status);
    }

    private async Task<int> CreateActivityAsync(string name)
    {
        var now = DateTime.UtcNow;
        var response = await _client.PostAsJsonAsync("/api/v1/admin/activities", new
        {
            activityName = name,
            description = "Kiểm thử hủy đăng ký hoạt động",
            organizerName = "Phòng CTSV",
            registrationOpenTime = now.AddHours(-1),
            registrationCloseTime = now.AddHours(6),
            maxParticipants = 10,
            trainingPoints = 5,
            targetAudience = "Tất cả sinh viên",
            status = "Published",
            sessions = new[]
            {
                new
                {
                    sessionName = "Buổi 1",
                    campus = "Cơ sở 1",
                    location = "Phòng A",
                    startTime = now.AddDays(1),
                    endTime = now.AddDays(1).AddHours(2),
                    sortOrder = 1
                }
            },
            audienceRules = Array.Empty<object>()
        });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<JsonElement> GetActivityDetailAsync(int activityId)
    {
        var response = await _client.GetAsync($"/api/v1/student-activities/{activityId}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private async Task AuthorizeAsync(string usernameOrEmail, string password)
    {
        var token = await LoginAsync(usernameOrEmail, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> LoginAsync(string usernameOrEmail, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            usernameOrEmail,
            password
        });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response did not contain a token.");
    }

    private static async Task<string?> ReadStringAsync(HttpResponseMessage response, string propertyName)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(propertyName).GetString();
    }
}
