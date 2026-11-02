using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

public sealed class ClassMonitorRelayTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly AccountTestClient _api;

    public ClassMonitorRelayTests(CustomWebApplicationFactory factory) => _api = new AccountTestClient(factory.CreateClient());

    [Fact]
    public async Task MonitorRelaysOfficialAnswerIntoClassThreadOnlyOnce()
    {
        var student = await _api.LoginAsync("SV210002", "123456");
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var threadId = await CreateQuestionAsync(student);
        var escalationResponse = await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/class-space/threads/{threadId}/escalations", monitor,
            new { question = "Xin xác nhận quy định tham gia sự kiện." });
        Assert.Equal(HttpStatusCode.Created, escalationResponse.StatusCode);
        var escalationId = (await AccountTestClient.ReadJsonAsync(escalationResponse)).GetProperty("data").GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/class-space/escalations/{escalationId}/relay", monitor, null)).StatusCode);

        var answer = "Sinh viên cần đăng ký trước hạn và check-in tại buổi tổ chức.";
        var staffAnswer = await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/class-escalations/{escalationId}/answer", staff, new { answer });
        Assert.Equal(HttpStatusCode.OK, staffAnswer.StatusCode);

        var relay = await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/escalations/{escalationId}/relay", monitor, null);
        Assert.Equal(HttpStatusCode.OK, relay.StatusCode);
        var relayed = (await AccountTestClient.ReadJsonAsync(relay)).GetProperty("data");
        Assert.False(relayed.GetProperty("canRelay").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, relayed.GetProperty("relayedAt").ValueKind);
        Assert.Equal("Nguyễn Văn Lớp Trưởng", relayed.GetProperty("relayedByName").GetString());

        var threadResponse = await _api.GetAsync($"/api/v1/class-space/threads/{threadId}", student);
        Assert.Equal(HttpStatusCode.OK, threadResponse.StatusCode);
        var thread = (await AccountTestClient.ReadJsonAsync(threadResponse)).GetProperty("data");
        Assert.Equal("Answered", thread.GetProperty("status").GetString());
        var officialReply = Assert.Single(thread.GetProperty("replies").EnumerateArray());
        Assert.True(officialReply.GetProperty("isAnswer").GetBoolean());
        Assert.Contains(answer, officialReply.GetProperty("body").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/class-space/escalations/{escalationId}/relay", monitor, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/class-space/escalations/{escalationId}/relay", student, null)).StatusCode);
    }

    private async Task<int> CreateQuestionAsync(string student)
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", student,
            new { kind = "Question", title = $"Câu hỏi cần xác nhận {Guid.NewGuid():N}", body = "Nội dung cần phòng phản hồi chính thức." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetInt32();
    }
}
