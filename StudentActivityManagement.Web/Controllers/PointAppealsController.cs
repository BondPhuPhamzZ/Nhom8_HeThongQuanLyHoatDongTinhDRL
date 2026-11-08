using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;

namespace StudentActivityManagement.Web.Controllers;

[Authorize(Policy = WebPolicies.Staff)]
public sealed class PointAppealsController : WebControllerBase
{
    private readonly ApiClient _api;
    public PointAppealsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(string? status = "Pending", int? periodId = null, int page = 1)
    {
        var periods = await _api.GetPeriodsAsync();
        var result = await _api.GetPointAppealsInboxAsync(status, periodId, Math.Max(page, 1));
        if (!periods.Success) FlashError(periods.Message);
        if (!result.Success) FlashError(result.Message);
        ViewBag.Status = status;
        ViewBag.PeriodId = periodId;
        ViewBag.Periods = periods.Data ?? [];
        return View(result.Data ?? new PagedResult<PointAppealDto>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, bool accept, string? response,
        string? status, int? periodId, int page = 1)
    {
        var trimmed = response?.Trim() ?? string.Empty;
        if (trimmed.Length is < 10 or > 1000)
            FlashError("Phản hồi phải dài từ 10 đến 1000 ký tự.");
        else
        {
            var result = await _api.ResolvePointAppealAsync(id, accept, trimmed);
            if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        }
        return RedirectToAction(nameof(Index), new { status, periodId, page });
    }
}
