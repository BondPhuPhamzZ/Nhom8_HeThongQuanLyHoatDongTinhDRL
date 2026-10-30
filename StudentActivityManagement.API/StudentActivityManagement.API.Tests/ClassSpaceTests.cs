using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// US-09: a class's private discussion space. SV210001 (monitor) and SV210002 share class 21DTH1; students of
/// 22DTH2 or without a class must not see it. Titles carry a Guid so tests sharing the database stay independent.
/// </summary>
public sealed class ClassSpaceTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "123456";
    private readonly AccountTestClient _api;

    public ClassSpaceTests(CustomWebApplicationFactory factory)
    {
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task Classmates_AskAndAnswer_InTheirOwnClassOnly()
    {
        var student = await _api.LoginAsync("SV210002", Password);
        var monitor = await _api.LoginAsync("SV210001", Password);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", student,
            new { kind = "question", title = $"Hỏi lịch sinh hoạt {tag}", body = "Tuần này lớp sinh hoạt ở phòng nào?" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var thread = await DataAsync(created);
        var threadId = thread.GetProperty("id").GetInt32();
        Assert.Equal("Question", thread.GetProperty("kind").GetString());
        Assert.Equal("Open", thread.GetProperty("status").GetString());
        Assert.True(thread.GetProperty("author").GetProperty("isMe").GetBoolean());
        Assert.True(thread.GetProperty("canDelete").GetBoolean());

        // The monitor sees the question in the class space and answers it.
        var space = await DataAsync(await _api.GetAsync($"/api/v1/class-space/threads?search={tag}", monitor));
        Assert.Equal("21DTH1", space.GetProperty("classCode").GetString());
        Assert.Equal("Nguyễn Văn Lớp Trưởng", space.GetProperty("monitorName").GetString());
        var listed = Assert.Single(space.GetProperty("threads").GetProperty("items").EnumerateArray());
        Assert.Equal(threadId, listed.GetProperty("id").GetInt32());
        Assert.False(listed.GetProperty("author").GetProperty("isMe").GetBoolean());

        var reply = await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{threadId}/replies", monitor,
            new { body = "Phòng B2.01 nhé." });
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        var replyData = await DataAsync(reply);
        Assert.True(replyData.GetProperty("author").GetProperty("isMonitor").GetBoolean());
        Assert.True(replyData.GetProperty("canDelete").GetBoolean());

        var detail = await DataAsync(await _api.GetAsync($"/api/v1/class-space/threads/{threadId}", student));
        Assert.Equal(1, detail.GetProperty("replyCount").GetInt32());
        Assert.Equal("Phòng B2.01 nhé.", Assert.Single(detail.GetProperty("replies").EnumerateArray()).GetProperty("body").GetString());
        Assert.False(detail.GetProperty("canDelete").GetBoolean());
        Assert.True(detail.GetProperty("lastActivityAt").GetDateTime() >= detail.GetProperty("createdAt").GetDateTime());

        // A student of another class cannot see, read or answer the thread.
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var outsider = await CreateStudentAsync(staff, await ClassIdAsync(staff, "22DTH2"));
        var otherSpace = await DataAsync(await _api.GetAsync($"/api/v1/class-space/threads?search={tag}", outsider));
        Assert.Equal("22DTH2", otherSpace.GetProperty("classCode").GetString());
        Assert.Equal(0, otherSpace.GetProperty("threads").GetProperty("totalCount").GetInt32());
        var hidden = await _api.GetAsync($"/api/v1/class-space/threads/{threadId}", outsider);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal("resource_not_found", await ErrorCodeAsync(hidden));
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{threadId}/replies", outsider,
            new { body = "Cho mình hỏi ké" })).StatusCode);
    }

    [Fact]
    public async Task List_FiltersByKindAndPutsLatestActivityFirst()
    {
        var student = await _api.LoginAsync("SV210002", Password);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var first = await CreateThreadAsync(student, "Question", $"Câu hỏi cũ {tag}");
        var second = await CreateThreadAsync(student, "Discussion", $"Thảo luận mới {tag}");

        var all = await ItemsAsync(student, $"search={tag}");
        Assert.Equal(new[] { second, first }, all);

        // Replying moves the older thread back to the top.
        Assert.Equal(HttpStatusCode.Created, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{first}/replies", student,
            new { body = "Bổ sung thêm" })).StatusCode);
        Assert.Equal(new[] { first, second }, await ItemsAsync(student, $"search={tag}"));

        Assert.Equal(new[] { second }, await ItemsAsync(student, $"search={tag}&kind=discussion"));
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.GetAsync("/api/v1/class-space/threads?kind=poll", student)).StatusCode);
    }

    [Fact]
    public async Task Create_RejectsAnnouncementsAndInvalidInput()
    {
        var student = await _api.LoginAsync("SV210002", Password);

        var announcement = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", student,
            new { kind = "Announcement", title = "Thông báo lớp", body = "Chỉ lớp trưởng mới được đăng." });
        Assert.Equal(HttpStatusCode.BadRequest, announcement.StatusCode);
        Assert.Equal("validation_failed", await ErrorCodeAsync(announcement));

        var shortTitle = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", student,
            new { kind = "Question", title = "Hi", body = "Nội dung" });
        Assert.Equal(HttpStatusCode.BadRequest, shortTitle.StatusCode);

        var blankBody = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", student,
            new { kind = "Question", title = "Câu hỏi trống", body = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blankBody.StatusCode);
    }

    [Fact]
    public async Task Delete_OnlyByAuthorAndBeforeOthersReply()
    {
        var student = await _api.LoginAsync("SV210002", Password);
        var monitor = await _api.LoginAsync("SV210001", Password);
        var tag = Guid.NewGuid().ToString("N")[..8];

        // The author's own follow-up does not block deleting the thread.
        var lonely = await CreateThreadAsync(student, "Discussion", $"Tự trả lời {tag}");
        Assert.Equal(HttpStatusCode.Created, (await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{lonely}/replies", student,
            new { body = "Tự bổ sung" })).StatusCode);
        var notAuthor = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/class-space/threads/{lonely}", monitor, null);
        Assert.Equal(HttpStatusCode.Forbidden, notAuthor.StatusCode);
        Assert.Equal("access_denied", await ErrorCodeAsync(notAuthor));
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/class-space/threads/{lonely}", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/api/v1/class-space/threads/{lonely}", student)).StatusCode);

        // Once a classmate answered, the thread stays; the classmate can still withdraw their own reply.
        var answered = await CreateThreadAsync(student, "Question", $"Đã có trả lời {tag}");
        var reply = await _api.SendAsync(HttpMethod.Post, $"/api/v1/class-space/threads/{answered}/replies", monitor, new { body = "Trả lời của lớp trưởng" });
        var replyId = (await DataAsync(reply)).GetProperty("id").GetInt32();
        var blocked = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/class-space/threads/{answered}", student, null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(blocked));

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/class-space/threads/{answered}/replies/{replyId}", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/class-space/threads/{answered}/replies/{replyId}", monitor, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/class-space/threads/{answered}", student, null)).StatusCode);
    }

    [Fact]
    public async Task Access_RequiresAClassAndAStudentRole()
    {
        var staff = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);
        var noClass = await CreateStudentAsync(staff, null);

        var unassigned = await _api.GetAsync("/api/v1/class-space/threads", noClass);
        Assert.Equal(HttpStatusCode.Conflict, unassigned.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(unassigned));
        Assert.Equal(HttpStatusCode.Conflict, (await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", noClass,
            new { kind = "Question", title = "Không có lớp", body = "Mình chưa có lớp" })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync("/api/v1/class-space/threads", staff)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync("/api/v1/class-space/threads", null)).StatusCode);
    }

    private async Task<int> CreateThreadAsync(string token, string kind, string title)
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/class-space/threads", token, new { kind, title, body = $"Nội dung: {title}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private async Task<int[]> ItemsAsync(string token, string query) =>
        (await DataAsync(await _api.GetAsync($"/api/v1/class-space/threads?{query}", token)))
            .GetProperty("threads").GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();

    private async Task<int> ClassIdAsync(string staff, string classCode) =>
        (await DataAsync(await _api.GetAsync("/api/v1/admin/classes", staff))).EnumerateArray()
            .Single(item => item.GetProperty("classCode").GetString() == classCode).GetProperty("id").GetInt32();

    private async Task<string> CreateStudentAsync(string staff, int? classId)
    {
        var code = $"SV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/students", staff, new
        {
            studentCode = code,
            fullName = "Sinh viên trao đổi lớp",
            email = $"{code.ToLowerInvariant()}@student.edu.vn",
            password = Password,
            campus = "Cơ sở 1",
            academicYear = "K22",
            classId
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
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
