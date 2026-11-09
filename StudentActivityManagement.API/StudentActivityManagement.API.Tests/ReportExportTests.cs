using System.Net;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Tests;

public sealed class ReportExportTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public ReportExportTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task ExportMatchesOverviewAndContainsAllThreeSheets()
    {
        var (periodId, activityEnd) = await CreatePeriodWithActivityAsync();
        var staff = await StaffAsync();
        var classId = await ClassIdAsync(staff);
        var query = $"periodId={periodId}&classId={classId}&groupBy=class";
        var overview = (await AccountTestClient.ReadJsonAsync(
            await _api.GetAsync($"/api/v1/admin/reports/overview?{query}", staff))).GetProperty("data");

        var response = await _api.GetAsync($"/api/v1/admin/reports/export?{query}", staff);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("BaoCao_Ky_", response.Content.Headers.ContentDisposition?.FileNameStar);

        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        Assert.Equal(3, workbook.Worksheets.Count);
        var summary = workbook.Worksheet("Tổng quan");
        var activities = workbook.Worksheet("Hoạt động");
        var atRisk = workbook.Worksheet("Sinh viên nguy cơ");
        Assert.Equal(overview.GetProperty("activityCount").GetInt32(), summary.Cell(7, 1).GetValue<int>());
        Assert.Equal(overview.GetProperty("studentCount").GetInt32(), summary.Cell(7, 3).GetValue<int>());
        Assert.Equal(overview.GetProperty("atRiskCount").GetInt32(), summary.Cell(7, 10).GetValue<int>());
        Assert.Equal("Hoạt động kiểm thử", activities.Cell(2, 2).GetString());
        Assert.Equal("Đã kết thúc", activities.Cell(2, 4).GetString());
        Assert.Equal(activityEnd.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
            activities.Cell(2, 5).GetDateTime().ToString("dd/MM/yyyy HH:mm"));
        Assert.Equal("0.0%", activities.Cell(2, 9).Style.NumberFormat.Format);

        var rows = atRisk.RowsUsed().Skip(1).ToList();
        Assert.Equal(overview.GetProperty("atRiskCount").GetInt32(), rows.Count);
        Assert.All(rows, row => Assert.Equal("21DTH1", row.Cell(3).GetString()));
    }

    [Fact]
    public async Task ExportRequiresStaffAndAnExistingPeriod()
    {
        var staff = await StaffAsync();
        var student = await _api.LoginAsync("SV210002", "123456");
        await AccountTestClient.AssertProblemAsync(
            await _api.GetAsync("/api/v1/admin/reports/export?periodId=999999", staff),
            HttpStatusCode.NotFound, "resource_not_found");
        await AccountTestClient.AssertProblemAsync(
            await _api.GetAsync("/api/v1/admin/reports/export", student),
            HttpStatusCode.Forbidden, "access_denied");
        await AccountTestClient.AssertProblemAsync(
            await _api.GetAsync("/api/v1/admin/reports/export", null),
            HttpStatusCode.Unauthorized, "authentication_required");
    }

    private async Task<(int PeriodId, DateTime ActivityEnd)> CreatePeriodWithActivityAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var period = new EvaluationPeriod
        {
            Name = $"Kỳ {Guid.NewGuid():N}", StartDate = now.AddDays(-1), EndDate = now.AddDays(1),
            TargetPoints = 10, Status = EvaluationPeriodStatus.Open, CreatedAt = now
        };
        var activity = new Activity
        {
            ActivityName = "Hoạt động kiểm thử", OrganizerName = "Phòng QL HSSV",
            StartTime = now.AddMinutes(5), EndTime = now.AddHours(1),
            RegistrationOpenTime = now.AddHours(-1), RegistrationCloseTime = now.AddMinutes(2),
            Status = ActivityStatus.Completed, TrainingPoints = 5
        };
        db.EvaluationPeriods.Add(period);
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        return (period.Id, activity.EndTime);
    }

    private Task<string> StaffAsync() =>
        _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    private async Task<int> ClassIdAsync(string staff) =>
        (await AccountTestClient.ReadJsonAsync(await _api.GetAsync("/api/v1/admin/classes", staff)))
            .GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("classCode").GetString() == "21DTH1")
            .GetProperty("id").GetInt32();
}
