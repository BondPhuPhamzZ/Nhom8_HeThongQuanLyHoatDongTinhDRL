using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

public sealed class ClassMonitorAnswersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly AccountTestClient _api;

    public ClassMonitorAnswersTests(CustomWebApplicationFactory factory) => _api = new AccountTestClient(factory.CreateClient());

    [Fact]
    public async Task MonitorCanSelectOneOfficialAnswerAndReplaceIt()
    {
        var student = await _api.LoginAsync("SV210002", "123456");
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var threadId = await CreateQuestionAsync(student);
        var firstReplyId = await CreateReplyAsync(monitor, threadId, "Câu trả lời ban đầu.");

        var markFirst = await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/class-space/threads/{threadId}/replies/{firstReplyId}/answer", monitor, null);
        Assert.Equal(HttpStatusCode.OK, markFirst.StatusCode);
        var answered = (await AccountTestClient.ReadJsonAsync(markFirst)).GetProperty("data");
        Assert.Equal("Answered", answered.GetProperty("status").GetString());
        Assert.True(answered.GetProperty("canManageAnswers").GetBoolean());
        Assert.True(answered.GetProperty("replies")[0].GetProperty("isAnswer").GetBoolean());

        var secondReplyId = await CreateReplyAsync(student, threadId, "Có thêm thông tin cập nhật.");
        var markSecond = await _api.SendAsync(HttpMethod.Post,
            $"/api/v1/class-space/threads/{threadId}/replies/{secondReplyId}/answer", monitor, null);
        Assert.Equal(HttpStatusCode.OK, markSecond.StatusCode);
        var replies = (await AccountTestClient.ReadJsonAsync(markSecond)).GetProperty("data").GetProperty("replies").EnumerateArray().ToList();
        Assert.Single(replies.Where(reply => reply.GetProperty("isAnswer").GetBoolean()));
        Assert.Equal(secondReplyId, replies.Single(reply => reply.GetProperty("isAnswer").GetBoolean()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task OnlyClassMonitorCanSelectAnswer_AndReplyMustBelongToQuestion()
    {
        var student = await _api.LoginAsync("SV210002", "123456");
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var threadId = await CreateQuestionAsync(student);
        var replyId = await CreateReplyAsync(student, threadId, "Giải đáp từ sinh viên.");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{threadId}/replies/{replyId}/answer", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{threadId}/replies/999999/answer", monitor, null)).StatusCode);
    }

    private async Task<int> CreateQuestionAsync(string student)
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", student,
            new { kind = "Question", title = $"Câu hỏi {Guid.NewGuid():N}", body = "Nội dung cần lớp hỗ trợ." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<int> CreateReplyAsync(string token, int threadId, string body)
    {
        var response = await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{threadId}/replies", token, new { body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetInt32();
    }
}
