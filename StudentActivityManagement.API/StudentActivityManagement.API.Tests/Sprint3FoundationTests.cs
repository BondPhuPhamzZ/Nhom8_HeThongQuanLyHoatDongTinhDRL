using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;
using StudentActivityManagement.API.Services.Storage;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// Building blocks that US-07/US-08/US-21/US-26 rely on: evidence file validation and storage,
/// the check-in settings provider and the protection of sessions that already hold attendance data.
/// </summary>
public sealed class Sprint3FoundationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public Sprint3FoundationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Theory]
    [InlineData("anh.PNG", EvidenceTypes.Image, ".png", "image/png")]
    [InlineData("scan.pdf", EvidenceTypes.Document, ".pdf", "application/pdf")]
    [InlineData("photo.jpeg", EvidenceTypes.Image, ".jpeg", "image/jpeg")]
    public void Validator_AcceptsMatchingExtensionAndSignature(string fileName, string type, string extension, string contentType)
    {
        var content = extension switch
        {
            ".png" => [.. PngHeader, 1, 2, 3],
            ".pdf" => Encoding.ASCII.GetBytes("%PDF-1.7 test"),
            _ => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2 }
        };

        var result = Validator().Validate(FormFile(fileName, content), type);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(extension, result.NormalizedExtension);
        Assert.Equal(contentType, result.ContentType);
    }

    [Theory]
    [InlineData("fake.png", EvidenceTypes.Image)]       // text renamed to .png
    [InlineData("scan.pdf", EvidenceTypes.Image)]       // pdf sent as an image
    [InlineData("anh.png", EvidenceTypes.Document)]     // png sent as a document
    [InlineData("script.exe", EvidenceTypes.Image)]
    public void Validator_RejectsWrongTypeOrSignature(string fileName, string type)
    {
        var content = fileName == "fake.png" ? Encoding.UTF8.GetBytes("not an image") : [.. PngHeader, 1];

        Assert.False(Validator().Validate(FormFile(fileName, content), type).IsValid);
    }

    [Fact]
    public void Validator_RejectsEmptyAndOversizedFiles()
    {
        var validator = Validator();
        var oversized = new byte[5 * 1024 * 1024 + 1];
        PngHeader.CopyTo(oversized, 0);

        Assert.False(validator.Validate(FormFile("a.png", []), EvidenceTypes.Image).IsValid);
        Assert.False(validator.Validate(FormFile("a.png", oversized), EvidenceTypes.Image).IsValid);
    }

    [Fact]
    public async Task Storage_SavesUnderGeneratedName_OutsideClientControl()
    {
        var storage = _factory.Services.GetRequiredService<IEvidenceFileStorage>();
        byte[] content = [.. PngHeader, 42];

        var storedName = await storage.SaveAsync(new MemoryStream(content), ".png");

        Assert.Matches("^[0-9a-f]{32}\\.png$", storedName);
        Assert.True(File.Exists(Path.Combine(_factory.EvidenceRootPath, storedName)));
        await using (var read = await storage.OpenReadAsync(storedName))
        {
            Assert.NotNull(read);
            using var copy = new MemoryStream();
            await read!.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
        }

        Assert.Null(await storage.OpenReadAsync("../appsettings.json"));
        Assert.Null(await storage.OpenReadAsync("..\\" + storedName));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(new MemoryStream(content), "/../x"));

        await storage.DeleteAsync(storedName);
        Assert.Null(await storage.OpenReadAsync(storedName));
    }

    [Fact]
    public async Task ConfigProvider_ReturnsDefaultsUntilARowExists()
    {
        var admin = await AdminAsync();
        var activityId = (await CreateActivityAsync(admin, sessionCount: 1)).GetProperty("id").GetInt32();

        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ICheckInConfigProvider>();
        var defaults = await provider.GetAsync(activityId);
        Assert.True(defaults.IsDefault);
        Assert.Equal(CheckInMethods.Code, defaults.Method);
        Assert.Equal(SettingDefaults.OpenBeforeMinutes, defaults.OpenBeforeMinutes);
        Assert.Equal(SettingDefaults.CloseAfterMinutes, defaults.CloseAfterMinutes);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.ActivityCheckInConfigs.Add(new ActivityCheckInConfig
        {
            ActivityId = activityId,
            Method = CheckInMethods.Manual,
            OpenBeforeMinutes = 0,
            CloseAfterMinutes = 60,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var stored = await provider.GetAsync(activityId);
        Assert.False(stored.IsDefault);
        Assert.Equal(CheckInMethods.Manual, stored.Method);

        var session = new ActivitySession { StartTime = DateTime.UtcNow.AddMinutes(30), EndTime = DateTime.UtcNow.AddHours(2) };
        Assert.False(defaults.IsOpen(session, DateTime.UtcNow));                 // opens 15 minutes before start
        Assert.True(defaults.IsOpen(session, DateTime.UtcNow.AddMinutes(20)));
        Assert.True(stored.IsOpen(session, session.EndTime.AddMinutes(59)));     // closes 60 minutes after end
        Assert.False(stored.IsOpen(session, session.EndTime.AddMinutes(61)));
    }

    [Fact]
    public async Task Update_CannotRemoveSessionThatHasCheckIns_AndCheckInIsUniquePerSession()
    {
        var admin = await AdminAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        var created = await CreateActivityAsync(admin, sessionCount: 2);
        var activityId = created.GetProperty("id").GetInt32();
        var first = SessionId(created, 1);
        var second = SessionId(created, 2);
        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/student-activities/{activityId}/register", student, null)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registrationId = await context.ActivityRegistrations
                .Where(registration => registration.ActivityId == activityId)
                .Select(registration => registration.Id)
                .SingleAsync();
            context.ActivityCheckIns.Add(new ActivityCheckIn { RegistrationId = registrationId, SessionId = second, CheckedInAt = DateTime.UtcNow });
            await context.SaveChangesAsync();

            context.ActivityCheckIns.Add(new ActivityCheckIn { RegistrationId = registrationId, SessionId = second, CheckedInAt = DateTime.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        var dropSecond = await UpdateAsync(admin, activityId, [SessionBody(first, 1)]);
        Assert.Equal(HttpStatusCode.Conflict, dropSecond.StatusCode);

        var keepSecond = await UpdateAsync(admin, activityId, [SessionBody(first, 1), SessionBody(second, 2)]);
        Assert.Equal(HttpStatusCode.OK, keepSecond.StatusCode);
    }

    private IEvidenceFileValidator Validator() => _factory.Services.GetRequiredService<IEvidenceFileValidator>();

    private static FormFile FormFile(string fileName, byte[] content) =>
        new(new MemoryStream(content), 0, content.Length, "file", fileName);

    private Task<string> AdminAsync() => _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    private async Task<JsonElement> CreateActivityAsync(string token, int sessionCount)
    {
        var sessions = Enumerable.Range(1, sessionCount).Select(order => SessionBody(null, order)).ToArray();
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/activities", token, ActivityBody(sessions));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private Task<HttpResponseMessage> UpdateAsync(string token, int id, object[] sessions) =>
        _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/activities/{id}", token, ActivityBody(sessions));

    private static object ActivityBody(object[] sessions) => new
    {
        activityName = $"Sprint3-{Guid.NewGuid():N}",
        description = "Kiểm thử nền Sprint 3",
        organizerName = "Phòng CTSV",
        registrationOpenTime = Iso(DateTime.UtcNow.AddHours(-1)),
        registrationCloseTime = Iso(DateTime.UtcNow.AddHours(6)),
        maxParticipants = 10,
        trainingPoints = 5,
        targetAudience = "Tất cả sinh viên",
        status = "Published",
        sessions,
        audienceRules = Array.Empty<object>()
    };

    private static object SessionBody(int? id, int sortOrder)
    {
        var start = DateTime.UtcNow.Date.AddDays(3 + sortOrder);
        return new
        {
            id,
            sessionName = $"Buổi {sortOrder}",
            campus = "Cơ sở 1",
            location = $"Phòng {sortOrder}",
            startTime = Iso(start),
            endTime = Iso(start.AddHours(2)),
            sortOrder
        };
    }

    private static int SessionId(JsonElement activity, int sortOrder) => activity.GetProperty("sessions").EnumerateArray()
        .First(session => session.GetProperty("sortOrder").GetInt32() == sortOrder).GetProperty("id").GetInt32();

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
