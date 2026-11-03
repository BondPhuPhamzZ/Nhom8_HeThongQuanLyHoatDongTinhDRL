using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

[Authorize(Policy = WebPolicies.Student)]
public sealed class StudentPointsController : WebControllerBase
{
    private readonly ApiClient _api;

    public StudentPointsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(int? periodId)
    {
        var result = await _api.GetStudentPointsRecommendationsAsync(periodId);
        // 404 only means no evaluation period exists yet; the view shows that as an empty state, not an error.
        if (!result.Success && result.StatusCode != System.Net.HttpStatusCode.NotFound) FlashError(result.Message);
        return View(new StudentPointsPageViewModel { Summary = result.Data, PeriodId = periodId });
    }
}
