using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

public sealed class StaffEscalationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly AccountTestClient _api;

    public StaffEscalationTests(CustomWebApplicationFactory factory) => _api = new AccountTestClient(factory.CreateClient());

    [Fact]
    public async Task Staff_InboxAndAnswer_VisibleToMonitor_AnswerIsFinal()
    {
        var student = await _api.LoginAsync("SV210002", "123456");
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var threadId = await CreateThreadAsync(student);
        var escalationId = await RaiseAsync(monitor, threadId);

        var inbox = await DataAsync(await _api.GetAsync("/api/v1/admin/class-escalations?status=pending", staff));
        Assert.Contains(inbox.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == escalationId);
        Assert.Equal("Pending", (await DataAsync(await _api.GetAsync($"/api/v1/admin/class-escalations/{escalationId}", staff))).GetProperty("status").GetString());

        var answer = await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/class-escalations/{escalationId}/answer", staff,
            new { answer = "  Phòng xác nhận hoạt động này được tính điểm.  " });
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var answered = await DataAsync(answer);
        Assert.Equal("Answered", answered.GetProperty("status").GetString());
        Assert.Equal("Phòng xác nhận hoạt động này được tính điểm.", answered.GetProperty("answer").GetString());
        Assert.Equal("Phòng Quản lý HSSV", answered.GetProperty("answeredByName").GetString());
        Assert.Equal(JsonValueKind.String, answered.GetProperty("answeredAt").ValueKind);

        var monitorList = await DataAsync(await _api.GetAsync("/api/v1/class-space/escalations?status=Answered", monitor));
        Assert.Contains(monitorList.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == escalationId
            && item.GetProperty("answer").GetString() == "Phòng xác nhận hoạt động này được tính điểm.");
        Assert.Equal(HttpStatusCode.Conflict,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/class-escalations/{escalationId}/answer", staff, new { answer = "Trả lời lại." })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/escalations/{escalationId}/withdraw", monitor, null)).StatusCode);
    }

    [Fact]
    public async Task Staff_AnswerRejectsWithdrawnAndInvalidInput_StudentsAreForbidden()
    {
        var student = await _api.LoginAsync("SV210002", "123456");
        var monitor = await _api.LoginAsync("SV210001", "123456");
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var admin = await _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var threadId = await CreateThreadAsync(student);
        var escalationId = await RaiseAsync(monitor, threadId);
        var url = $"/api/v1/admin/class-escalations/{escalationId}/answer";

        await AccountTestClient.AssertProblemAsync(await _api.GetAsync("/api/v1/admin/class-escalations", null), HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync("/api/v1/admin/class-escalations", student)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Post, url, monitor, new { answer = "Không có quyền trả lời." })).StatusCode);
        await AccountTestClient.AssertProblemAsync(await _api.SendAsync(HttpMethod.Post, url, staff, new { answer = "    " }),
            HttpStatusCode.BadRequest, "validation_failed");
        await AccountTestClient.AssertProblemAsync(await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/class-escalations/999999/answer", staff,
            new { answer = "Câu trả lời hợp lệ." }), HttpStatusCode.NotFound, "resource_not_found");
        await AccountTestClient.AssertProblemAsync(await _api.GetAsync("/api/v1/admin/class-escalations?status=Unknown", staff),
            HttpStatusCode.BadRequest, "validation_failed");

        Assert.Equal(HttpStatusCode.OK,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/escalations/{escalationId}/withdraw", monitor, null)).StatusCode);
        await AccountTestClient.AssertProblemAsync(await _api.SendAsync(HttpMethod.Post, url, admin, new { answer = "Trả lời sau rút lại." }),
            HttpStatusCode.Conflict, "business_rule_violation");
    }

    private async Task<int> CreateThreadAsync(string student)
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", student,
            new { kind = "Question", title = $"Cần xác nhận {Guid.NewGuid():N}", body = "Nhờ lớp trưởng hỏi Phòng QL HSSV." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private async Task<int> RaiseAsync(string monitor, int threadId)
    {
        var response = await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{threadId}/escalations", monitor,
            new { question = "Nhờ phòng xác nhận quy định tính điểm cho lớp." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
