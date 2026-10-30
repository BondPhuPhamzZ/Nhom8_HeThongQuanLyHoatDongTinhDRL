using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-24: period report for Phòng QL HSSV (overview, breakdown, activities, at-risk students).</summary>
[Authorize(Policy = WebPolicies.Staff)]
public sealed class ReportsController : WebControllerBase
{
    private readonly ApiClient _api;

    public ReportsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index([FromQuery] ReportFilter filter, int page = 1)
    {
        var model = new ReportViewModel
        {
            Filter = filter,
            Periods = (await _api.GetPeriodsAsync()).Data ?? [],
            Classes = (await _api.GetClassesAsync()).Data ?? []
        };

        var overview = await _api.GetReportOverviewAsync(filter);
        if (!overview.Success || overview.Data == null)
        {
            // No period yet is the normal first-run case; the view explains it instead of an error banner.
            if (overview.StatusCode != System.Net.HttpStatusCode.NotFound || filter.PeriodId.HasValue)
            {
                FlashError(overview.Message);
            }
            return View(model);
        }

        model.Overview = overview.Data;
        // Later queries use the period the API picked, so all three sections describe the same period.
        filter.PeriodId ??= overview.Data.Period.Id;
        model.Activities = (await _api.GetActivityReportAsync(filter)).Data ?? [];
        model.AtRisk = (await _api.GetAtRiskStudentsAsync(filter, Math.Max(page, 1))).Data ?? new();
        return View(model);
    }
}
