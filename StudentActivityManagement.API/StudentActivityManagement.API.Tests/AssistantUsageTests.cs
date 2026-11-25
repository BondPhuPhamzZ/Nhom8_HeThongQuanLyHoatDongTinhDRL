using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services.Assistant;

namespace StudentActivityManagement.API.Tests;

public sealed class AssistantUsageTests
{
    [Fact]
    public async Task Chat_TokenLimit_BlocksBeforeCallingGeminiAgain()
    {
        using var factory = new CustomWebApplicationFactory();
        var gemini = new ScriptedGeminiHandler();
        gemini.Reply(WithTokens(ScriptedGeminiHandler.Text("Đây là câu trả lời."), 19, 9));
        using var app = CreateApp(factory, gemini, ("Assistant:DailyTokenLimit", "20"));
        var api = new AccountTestClient(app.CreateClient());
        var student = await StudentAsync(api);
        Assert.Equal(HttpStatusCode.OK, (await AskAsync(api, student)).StatusCode);
        var status = await StatusAsync(api, student);
        Assert.Equal(28, status.GetProperty("tokensUsedToday").GetInt32());
        Assert.Equal(20, status.GetProperty("dailyTokenLimit").GetInt32());
        await AssertLimitedAsync(await AskAsync(api, student));
        Assert.Single(gemini.Requests);
    }

    [Fact]
    public async Task ReportInsights_StaffDailyLimit_StopsTheSecondRequest()
    {
        using var factory = new CustomWebApplicationFactory();
        var gemini = new ScriptedGeminiHandler();
        gemini.Reply(Insights());
        using var app = CreateApp(factory, gemini, ("Assistant:StaffDailyRequestLimit", "1"));
        var api = new AccountTestClient(app.CreateClient());
        var staff = await StaffAsync(api);
        var periodId = await PeriodAsync(api, staff);
        Assert.Equal(HttpStatusCode.OK, (await InsightsAsync(api, staff, periodId)).StatusCode);
        await AssertLimitedAsync(await InsightsAsync(api, staff, periodId));
        Assert.Single(gemini.Requests);
    }

    [Fact]
    public async Task GlobalLimit_CountsBothFeaturesAndHidesChatFromUsageReport()
    {
        using var factory = new CustomWebApplicationFactory();
        var gemini = new ScriptedGeminiHandler();
        gemini.Reply(WithTokens(ScriptedGeminiHandler.Text("Bạn có thể xem điểm trong ứng dụng."), 12, 7));
        gemini.Reply(WithTokens(Insights(), 8, 3));
        using var app = CreateApp(factory, gemini, ("Assistant:GlobalDailyRequestLimit", "2"));
        var api = new AccountTestClient(app.CreateClient());
        var staff = await StaffAsync(api);
        var student = await StudentAsync(api);
        var monitor = await api.LoginAsync("SV210001", "123456");
        var admin = await api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);
        var period = await PeriodAsync(api, staff);

        Assert.Equal(HttpStatusCode.OK, (await AskAsync(api, student)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await InsightsAsync(api, staff, period)).StatusCode);
        var limited = await AskAsync(api, monitor);
        await AssertLimitedAsync(limited);
        Assert.Contains("giới hạn sử dụng chung", (await AccountTestClient.ReadJsonAsync(limited)).GetProperty("detail").GetString());
        Assert.Equal(2, gemini.Requests.Count);

        const string path = "/api/v1/admin/assistant-usage";
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync(path, staff)).StatusCode);
        var report = await api.GetAsync(path, admin);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var data = (await AccountTestClient.ReadJsonAsync(report)).GetProperty("data");
        var rows = data.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Contains(rows, row => row.GetProperty("feature").GetString() == "StaffAi" && row.GetProperty("tokenCount").GetInt32() == 11);
        Assert.Contains(rows, row => row.GetProperty("feature").GetString() == "Assistant" && row.GetProperty("tokenCount").GetInt32() == 19);
        Assert.All(rows, row =>
        {
            Assert.False(row.TryGetProperty("content", out _));
            Assert.False(row.TryGetProperty("conversationId", out _));
        });
        var total = Assert.Single(data.GetProperty("dailyTotals").EnumerateArray());
        Assert.Equal(30, total.GetProperty("tokenCount").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, (await api.GetAsync(path + "?from=2026-01-01&to=2026-02-01", admin)).StatusCode);
    }

    [Fact]
    public async Task Chat_FailureAfterToolRound_RetainsOneRequestAndTokens()
    {
        using var factory = new CustomWebApplicationFactory();
        var gemini = new ScriptedGeminiHandler();
        gemini.ReplyStatus(HttpStatusCode.ServiceUnavailable, "{}");
        gemini.Reply(WithTokens(ScriptedGeminiHandler.FunctionCall(AssistantTools.ListEvaluationPeriods, new JsonObject(), null), 21, 4));
        gemini.ReplyStatus(HttpStatusCode.ServiceUnavailable, "{}");
        using var app = CreateApp(factory, gemini, ("Assistant:DailyQuestionLimit", "2"));
        var api = new AccountTestClient(app.CreateClient());
        var student = await StudentAsync(api);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await AskAsync(api, student)).StatusCode);
        Assert.Equal(2, (await StatusAsync(api, student)).GetProperty("remainingToday").GetInt32());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await AskAsync(api, student)).StatusCode);
        var status = await StatusAsync(api, student);
        Assert.Equal(1, status.GetProperty("remainingToday").GetInt32());
        Assert.Equal(25, status.GetProperty("tokensUsedToday").GetInt32());
        Assert.Equal(3, gemini.Requests.Count);
    }

    [Fact]
    public async Task Status_ReloadAndNewConversation_PreserveQuotaUntilVietnamMidnight()
    {
        using var factory = new CustomWebApplicationFactory();
        using var time = factory.Time.At(new DateTime(2026, 11, 25, 16, 55, 0, DateTimeKind.Utc));
        var gemini = new ScriptedGeminiHandler();
        gemini.ReplyAlways(WithTokens(ScriptedGeminiHandler.Text("Mình đã nhận câu hỏi."), 6, 2));
        using var app = CreateApp(factory, gemini, ("Assistant:DailyQuestionLimit", "4"));
        var api = new AccountTestClient(app.CreateClient());
        var student = await StudentAsync(api);
        var start = (await AccountTestClient.ReadJsonAsync(await AskAsync(api, student))).GetProperty("data");
        var id = start.GetProperty("conversationId").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await AskAsync(api, student, id)).StatusCode);
        Assert.Equal(2, (await StatusAsync(api, student)).GetProperty("remainingToday").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/v1/assistant/conversations", student)).StatusCode);
        Assert.Equal(2, (await StatusAsync(api, student)).GetProperty("remainingToday").GetInt32());

        var newer = (await AccountTestClient.ReadJsonAsync(await AskAsync(api, student))).GetProperty("data");
        Assert.NotEqual(id, newer.GetProperty("conversationId").GetInt32());
        Assert.Equal(1, newer.GetProperty("remainingToday").GetInt32());
        Assert.Equal(24, newer.GetProperty("tokensUsedToday").GetInt32());

        factory.Time.Advance(TimeSpan.FromMinutes(6));
        var nextDay = await StatusAsync(api, student);
        Assert.Equal(4, nextDay.GetProperty("remainingToday").GetInt32());
        Assert.Equal(0, nextDay.GetProperty("tokensUsedToday").GetInt32());
    }

    private static string WithTokens(string response, int prompt, int output)
    {
        var node = JsonNode.Parse(response)!;
        node["usageMetadata"] = new JsonObject { ["promptTokenCount"] = prompt, ["candidatesTokenCount"] = output };
        return node.ToJsonString();
    }

    private static WebApplicationFactory<Program> CreateApp(CustomWebApplicationFactory factory, ScriptedGeminiHandler gemini,
        params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-key",
            ["Gemini:Model"] = "gemini-test",
            ["Gemini:BaseUrl"] = "https://gemini.test/v1beta/",
            ["Assistant:RequestsPerMinute"] = "100",
            ["Assistant:Enabled"] = "true"
        };
        foreach (var (key, value) in overrides) values[key] = value;
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureTestServices(services => services.AddHttpClient<GeminiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => gemini));
        });
    }

    private static Task<string> StudentAsync(AccountTestClient api) => api.LoginAsync("SV210002", "123456");

    private static Task<string> StaffAsync(AccountTestClient api) =>
        api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    private static Task<HttpResponseMessage> AskAsync(AccountTestClient api, string student, int? conversationId = null) =>
        api.SendAsync(HttpMethod.Post, "/api/v1/assistant/chat", student, new { conversationId, message = "Điểm kỳ này ra sao?" });

    private static Task<HttpResponseMessage> InsightsAsync(AccountTestClient api, string staff, int periodId) =>
        api.SendAsync(HttpMethod.Post, $"/api/v1/admin/reports/ai-insights?periodId={periodId}", staff, null);

    private static async Task<int> PeriodAsync(AccountTestClient api, string staff)
    {
        var response = await api.SendAsync(HttpMethod.Post, "/api/v1/admin/evaluation-periods", staff,
            new { name = "Kỳ thống kê AI", startDate = "2049-01-01T00:00:00Z", endDate = "2049-06-30T00:00:00Z", targetPoints = 40 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> StatusAsync(AccountTestClient api, string student) =>
        (await AccountTestClient.ReadJsonAsync(await api.GetAsync("/api/v1/assistant/status", student))).GetProperty("data");

    private static string Insights() => ScriptedGeminiHandler.Text("""{"highlights":["Kỳ đang mở"],"concerns":[],"suggestions":[]}""");

    private static Task AssertLimitedAsync(HttpResponseMessage response) =>
        AccountTestClient.AssertProblemAsync(response, HttpStatusCode.TooManyRequests, ApiErrorCodes.AssistantRateLimited);
}
