using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

[Authorize(Policy = WebPolicies.Student)]
public sealed class StudentReportsController : WebControllerBase
{
    private readonly ApiClient _api;

    public StudentReportsController(ApiClient api) => _api = api;

    public async Task<IActionResult> History(int? periodId, string? registrationStatus)
    {
        var periods = await _api.GetStudentReportPeriodsAsync();
        var model = new StudentEvaluationHistoryPageViewModel
        {
            Periods = periods.Data ?? [],
            PeriodId = periodId,
            RegistrationStatus = registrationStatus
        };
        if (!periods.Success)
        {
            FlashError(periods.Message);
            return View(model);
        }
        model.PeriodId ??= model.Periods.FirstOrDefault(period => period.Status == "Open")?.Id
            ?? model.Periods.FirstOrDefault()?.Id;
        if (model.PeriodId.HasValue)
        {
            var result = await _api.GetStudentActivityHistoryAsync(model.PeriodId, registrationStatus);
            if (result.Success) model.History = result.Data; else FlashError(result.Message);
            var appeals = await _api.GetMyPointAppealsAsync();
            if (appeals.Success && appeals.Data != null)
                model.LatestAppeals = appeals.Data.GroupBy(item => item.PointRecordId)
                    .ToDictionary(group => group.Key, group => group.First());
        }
        return View(model);
    }

    /// <summary>US-37: one place to see which decisions can be appealed now and follow every appeal sent.</summary>
    public async Task<IActionResult> Appeals(int? periodId)
    {
        var periods = await _api.GetStudentReportPeriodsAsync();
        var model = new StudentAppealsPageViewModel { Periods = periods.Data ?? [], PeriodId = periodId };
        if (!periods.Success) FlashError(periods.Message);
        model.PeriodId ??= model.Periods.FirstOrDefault(period => period.Status == "Open")?.Id
            ?? model.Periods.FirstOrDefault()?.Id;
        if (model.PeriodId.HasValue)
        {
            var history = await _api.GetStudentActivityHistoryAsync(model.PeriodId, null);
            if (history.Success && history.Data != null)
                model.Appealable = history.Data.Activities.Where(activity => activity.CanAppeal).ToList();
            else if (!history.Success) FlashError(history.Message);
        }
        var appeals = await _api.GetMyPointAppealsAsync();
        if (appeals.Success) model.Appeals = appeals.Data ?? []; else FlashError(appeals.Message);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Appeal(int pointRecordId, string? reason, int? periodId, string? registrationStatus, string? from)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length is < 10 or > 1000)
            FlashError("Lý do khiếu nại phải dài từ 10 đến 1000 ký tự.");
        else
        {
            var result = await _api.CreatePointAppealAsync(pointRecordId, trimmed);
            if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        }
        return BackTo(from, periodId, registrationStatus);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawAppeal(int id, int? periodId, string? registrationStatus, string? from)
    {
        var result = await _api.WithdrawPointAppealAsync(id);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return BackTo(from, periodId, registrationStatus);
    }

    private IActionResult BackTo(string? from, int? periodId, string? registrationStatus) => from == "appeals"
        ? RedirectToAction(nameof(Appeals), new { periodId })
        : RedirectToAction(nameof(History), new { periodId, registrationStatus });
}
