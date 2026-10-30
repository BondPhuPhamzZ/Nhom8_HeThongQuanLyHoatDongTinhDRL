using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-20: Phòng QL HSSV receives and answers questions raised by class monitors.</summary>
[Authorize(Policy = WebPolicies.Staff)]
public sealed class StaffEscalationsController : WebControllerBase
{
    private readonly ApiClient _api;

    public StaffEscalationsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(string? status = "Pending", int page = 1)
    {
        var result = await _api.GetStaffEscalationsAsync(status, Math.Max(page, 1));
        if (!result.Success) FlashError(result.Message);
        ViewBag.Status = status;
        return View(result.Data ?? new PagedResult<ThreadEscalation>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(int id, string? answer, string? status, int page = 1)
    {
        var trimmed = answer?.Trim() ?? string.Empty;
        if (trimmed.Length is < 5 or > 4000)
        {
            FlashError("Phản hồi từ 5 đến 4000 ký tự.");
            return RedirectToAction(nameof(Index), new { status, page });
        }

        var result = await _api.AnswerStaffEscalationAsync(id, trimmed);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Index), new { status, page });
    }
}
