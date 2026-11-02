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
        }
        return View(model);
    }
}
