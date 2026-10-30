using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-22: Phòng QL HSSV defines evaluation periods and closes them when the semester is final.</summary>
[Authorize(Policy = WebPolicies.Staff)]
public sealed class EvaluationPeriodsController : WebControllerBase
{
    private readonly ApiClient _api;

    public EvaluationPeriodsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetPeriodsAsync();
        if (!result.Success)
        {
            FlashError(result.Message);
        }

        return View(new PeriodListViewModel { Periods = result.Data ?? [] });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(PeriodListViewModel.NewPeriod))] PeriodCreateForm form)
    {
        if (ModelState.IsValid)
        {
            // Dates are Vietnam calendar days: the period runs from 00:00 of the first day to 23:59:59 of the last.
            var result = await _api.CreatePeriodAsync(
                form.Name.Trim(),
                VnTime.ToOffset(form.StartDate.Date),
                VnTime.ToOffset(form.EndDate.Date.AddDays(1).AddSeconds(-1)),
                form.TargetPoints);
            if (result.Success)
            {
                FlashSuccess(result.Message);
                return RedirectToAction(nameof(Index));
            }

            AddApiErrors(result);
        }

        var periods = await _api.GetPeriodsAsync();
        return View(nameof(Index), new PeriodListViewModel { Periods = periods.Data ?? [], NewPeriod = form });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, string status)
    {
        var result = await _api.SetPeriodStatusAsync(id, status);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Index));
    }
}
