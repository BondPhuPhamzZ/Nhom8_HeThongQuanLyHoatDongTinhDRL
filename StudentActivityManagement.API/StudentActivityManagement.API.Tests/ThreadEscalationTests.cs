using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// US-15: the class monitor (SV210001 of 21DTH1) raises a class thread to Phòng QL HSSV. Classmates only see the
/// status; students, staff and monitors of other classes cannot raise, list or withdraw 21DTH1's escalations.
/// </summary>
public sealed class ThreadEscalationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "123456";
    private readonly AccountTestClient _api;

    public ThreadEscalationTests(CustomWebApplicationFactory factory)
    {
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task Monitor_RaisesWithdrawsAndRaisesAgain_ClassSeesTheStatus()
    {
        var student = await _api.LoginAsync("SV210002", Password);
        var monitor = await _api.LoginAsync("SV210001", Password);
        var threadId = await CreateThreadAsync(student, $"Điểm hiến máu có tính không {Guid.NewGuid():N}");

        Assert.False((await ThreadAsync(student, threadId)).GetProperty("canEscalate").GetBoolean());
        Assert.True((await ThreadAsync(monitor, threadId)).GetProperty("canEscalate").GetBoolean());

        var raised = await RaiseAsync(monitor, threadId, "Nhờ phòng xác nhận hoạt động hiến máu có được cộng điểm rèn luyện không?");
        Assert.Equal(HttpStatusCode.Created, raised.StatusCode);
        var escalation = await DataAsync(raised);
        var firstId = escalation.GetProperty("id").GetInt32();
        Assert.Equal("Pending", escalation.GetProperty("status").GetString());
        Assert.Equal(threadId, escalation.GetProperty("threadId").GetInt32());
        Assert.Equal("Nguyễn Văn Lớp Trưởng", escalation.GetProperty("raisedByName").GetString());
        Assert.Equal(JsonValueKind.Null, escalation.GetProperty("answer").ValueKind);

        // Classmates see that the question went to Phòng QL HSSV; the author can no longer delete it.
        var seen = await ThreadAsync(student, threadId);
        Assert.Equal("Pending", seen.GetProperty("escalationStatus").GetString());
        Assert.False(seen.GetProperty("canDelete").GetBoolean());
        var deleted = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/class-space/threads/{threadId}", student, null);
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(deleted));
        var listed = (await DataAsync(await _api.GetAsync("/api/v1/class-space/threads?pageSize=50", student)))
            .GetProperty("threads").GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == threadId);
        Assert.Equal("Pending", listed.GetProperty("escalationStatus").GetString());

        // One pending escalation per thread.
        Assert.False((await ThreadAsync(monitor, threadId)).GetProperty("canEscalate").GetBoolean());
        var twice = await RaiseAsync(monitor, threadId, "Hỏi lại lần nữa");
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(twice));

        var space = await DataAsync(await _api.GetAsync("/api/v1/class-space/threads", monitor));
        Assert.True(space.GetProperty("isMonitor").GetBoolean());
        Assert.Contains(await EscalationIdsAsync(monitor, "status=pending"), id => id == firstId);

        // Withdraw ends the escalation; it cannot be withdrawn twice, and the thread can be raised again.
        var withdrawn = await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/escalations/{firstId}/withdraw", monitor, null);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        var withdrawnData = await DataAsync(withdrawn);
        Assert.Equal("Withdrawn", withdrawnData.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, withdrawnData.GetProperty("withdrawnAt").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/escalations/{firstId}/withdraw", monitor, null)).StatusCode);
        Assert.Equal("Withdrawn", (await ThreadAsync(student, threadId)).GetProperty("escalationStatus").GetString());
        Assert.DoesNotContain(await EscalationIdsAsync(monitor, "status=Pending"), id => id == firstId);

        var again = await RaiseAsync(monitor, threadId, "Phòng xác nhận giúp lớp: hiến máu được bao nhiêu điểm?");
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var secondId = (await DataAsync(again)).GetProperty("id").GetInt32();
        var all = await EscalationIdsAsync(monitor, "pageSize=50");
        Assert.True(all.IndexOf(secondId) < all.IndexOf(firstId), "Newest escalation comes first.");
    }

    [Fact]
    public async Task Escalations_BelongToTheClassMonitorOnly()
    {
        var student = await _api.LoginAsync("SV210002", Password);
        var monitor = await _api.LoginAsync("SV210001", Password);
        var threadId = await CreateThreadAsync(student, $"Hỏi lịch thi {Guid.NewGuid():N}");
        var escalationId = (await DataAsync(await RaiseAsync(monitor, threadId, "Nhờ phòng xác nhận lịch thi bù."))).GetProperty("id").GetInt32();

        var byStudent = await RaiseAsync(student, threadId, "Sinh viên tự chuyển lên");
        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal("access_denied", await ErrorCodeAsync(byStudent));
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync("/api/v1/class-space/escalations", student)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/escalations/{escalationId}/withdraw", student, null)).StatusCode);

        // The monitor of another class sees neither the thread nor the escalation.
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var otherMonitor = await CreateMonitorAsync(staff, "22DTH2");
        Assert.Equal(HttpStatusCode.NotFound, (await RaiseAsync(otherMonitor, threadId, "Chuyển hộ lớp khác")).StatusCode);
        Assert.DoesNotContain(await EscalationIdsAsync(otherMonitor, "pageSize=50"), id => id == escalationId);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/escalations/{escalationId}/withdraw", otherMonitor, null)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await RaiseAsync(staff, threadId, "Cán bộ không dùng không gian lớp")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync("/api/v1/class-space/escalations", null)).StatusCode);
    }

    [Fact]
    public async Task Escalate_ValidatesInput()
    {
        var student = await _api.LoginAsync("SV210002", Password);
        var monitor = await _api.LoginAsync("SV210001", Password);
        var threadId = await CreateThreadAsync(student, $"Hỏi học bổng {Guid.NewGuid():N}");

        var tooShort = await RaiseAsync(monitor, threadId, "Hỏi");
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal("validation_failed", await ErrorCodeAsync(tooShort));
        Assert.Equal(HttpStatusCode.BadRequest, (await RaiseAsync(monitor, threadId, "     ")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RaiseAsync(monitor, 999999, "Chủ đề không tồn tại")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.GetAsync("/api/v1/class-space/escalations?status=Closed", monitor)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/escalations/999999/withdraw", monitor, null)).StatusCode);
    }

    private Task<HttpResponseMessage> RaiseAsync(string? token, int threadId, string question) =>
        _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{threadId}/escalations", token, new { question });

    private async Task<int> CreateThreadAsync(string token, string title)
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", token,
            new { kind = "Question", title, body = $"Nội dung: {title}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private async Task<JsonElement> ThreadAsync(string token, int threadId) =>
        await DataAsync(await _api.GetAsync($"/api/v1/class-space/threads/{threadId}", token));

    private async Task<List<int>> EscalationIdsAsync(string token, string query) =>
        (await DataAsync(await _api.GetAsync($"/api/v1/class-space/escalations?{query}", token)))
            .GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToList();

    /// <summary>New student made monitor of the class; logs in after the assignment so the token carries the Monitor role.</summary>
    private async Task<string> CreateMonitorAsync(string staff, string classCode)
    {
        var classId = (await DataAsync(await _api.GetAsync("/api/v1/admin/classes", staff))).EnumerateArray()
            .Single(item => item.GetProperty("classCode").GetString() == classCode).GetProperty("id").GetInt32();
        var code = $"SV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/students", staff, new
        {
            studentCode = code,
            fullName = "Lớp trưởng lớp khác",
            email = $"{code.ToLowerInvariant()}@student.edu.vn",
            password = Password,
            campus = "Cơ sở 1",
            academicYear = "K22",
            classId
        });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var studentId = (await DataAsync(created)).GetProperty("id").GetInt32();
        var assigned = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/classes/{classId}/monitor/{studentId}", staff, null);
        Assert.True(assigned.IsSuccessStatusCode, await assigned.Content.ReadAsStringAsync());
        return await _api.LoginAsync(code, Password);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errorCode").GetString();
    }
}
