using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

public sealed class ClassMonitorAnnouncementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly AccountTestClient _api;

    public ClassMonitorAnnouncementTests(CustomWebApplicationFactory factory) => _api = new AccountTestClient(factory.CreateClient());

    [Fact]
    public async Task CurrentMonitor_CanPostPinnedAnnouncementAndChangePinState()
    {
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var student = await _api.LoginAsync("SV210002", "123456");
        var create = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", monitor, new
        {
            kind = "Announcement",
            title = "Nhắc lịch sinh hoạt lớp",
            body = "Cả lớp có mặt lúc 9 giờ tại phòng A."
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var announcement = (await AccountTestClient.ReadJsonAsync(create)).GetProperty("data");
        var threadId = announcement.GetProperty("id").GetInt32();
        Assert.Equal("Announcement", announcement.GetProperty("kind").GetString());
        Assert.True(announcement.GetProperty("isPinned").GetBoolean());
        Assert.True(announcement.GetProperty("canPin").GetBoolean());

        var visible = await _api.GetAsync("/api/v1/class-space/threads?kind=Announcement", student);
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        Assert.Equal(threadId, (await AccountTestClient.ReadJsonAsync(visible)).GetProperty("data")
            .GetProperty("threads").GetProperty("items")[0].GetProperty("id").GetInt32());

        var unpin = await _api.SendAsync(HttpMethod.Patch, $"/api/v1/class-space/threads/{threadId}/pin", monitor, new { isPinned = false });
        Assert.Equal(HttpStatusCode.OK, unpin.StatusCode);
        Assert.False((await AccountTestClient.ReadJsonAsync(unpin)).GetProperty("data").GetProperty("isPinned").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _api.SendAsync(HttpMethod.Patch, $"/api/v1/class-space/threads/{threadId}/pin", student, new { isPinned = true })).StatusCode);
    }

    [Fact]
    public async Task OnlyCurrentClassMonitor_CanCreateAnnouncement()
    {
        var student = await _api.LoginAsync("SV210002", "123456");
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", student, new
        {
            kind = "Announcement",
            title = "Thông báo không hợp lệ",
            body = "Sinh viên không được thay lớp trưởng đăng thông báo."
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
