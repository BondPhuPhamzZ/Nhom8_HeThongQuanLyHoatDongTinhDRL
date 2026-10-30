using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>US-22: evaluation periods. Each test uses its own year so periods never overlap across tests.</summary>
public sealed class EvaluationPeriodTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Url = "/api/v1/admin/evaluation-periods";
    private readonly AccountTestClient _api;

    public EvaluationPeriodTests(CustomWebApplicationFactory factory)
    {
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task Create_List_UpdateAndClose()
    {
        var staff = await StaffAsync();
        var created = await _api.SendAsync(HttpMethod.Post, Url, staff, Body("HK1 2031", 2031, 50));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var period = await DataAsync(created);
        var id = period.GetProperty("id").GetInt32();
        Assert.Equal("Open", period.GetProperty("status").GetString());
        Assert.EndsWith("Z", period.GetProperty("startDate").GetString());

        var list = await DataAsync(await _api.GetAsync(Url, staff));
        Assert.Contains(list.EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);

        var renamed = await _api.SendAsync(HttpMethod.Put, $"{Url}/{id}", staff, Body("HK1 2031-2032", 2031, 60));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(60, (await DataAsync(renamed)).GetProperty("targetPoints").GetInt32());

        var closed = await _api.SendAsync(HttpMethod.Patch, $"{Url}/{id}/status", staff, new { status = "closed" });
        Assert.Equal("Closed", (await DataAsync(closed)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _api.SendAsync(HttpMethod.Patch, $"{Url}/{id}/status", staff, new { status = "Archived" })).StatusCode);
    }

    [Fact]
    public async Task Create_RejectsDuplicateNameOverlapAndBadRange()
    {
        var staff = await StaffAsync();
        Assert.Equal(HttpStatusCode.Created, (await _api.SendAsync(HttpMethod.Post, Url, staff, Body("HK1 2032", 2032, 50))).StatusCode);

        var duplicate = await _api.SendAsync(HttpMethod.Post, Url, staff, Body("HK1 2032", 2033, 50));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var overlap = await _api.SendAsync(HttpMethod.Post, Url, staff, new
        {
            name = "Chồng thời gian 2032",
            startDate = "2032-03-01T00:00:00Z",
            endDate = "2032-09-01T00:00:00Z",
            targetPoints = 50
        });
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);

        var backwards = await _api.SendAsync(HttpMethod.Post, Url, staff, new
        {
            name = "Ngược 2034",
            startDate = "2034-06-01T00:00:00Z",
            endDate = "2034-01-01T00:00:00Z",
            targetPoints = 50
        });
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
    }

    [Fact]
    public async Task Periods_RequireBusinessAdmin()
    {
        var student = await _api.LoginAsync("SV210002", "123456");

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync(Url, student)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync(Url, null)).StatusCode);
    }

    private Task<string> StaffAsync() => _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    /// <summary>First half of <paramref name="year"/>.</summary>
    private static object Body(string name, int year, int targetPoints) => new
    {
        name,
        startDate = $"{year}-01-01T00:00:00Z",
        endDate = $"{year}-06-30T23:59:59Z",
        targetPoints
    };

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
