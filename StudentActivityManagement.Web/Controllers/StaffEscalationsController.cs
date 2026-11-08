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

    /// <summary>US-34: AI draft for a pending question. Nothing is saved; the page puts the draft in the answer box.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Draft(int id)
    {
        var result = await _api.DraftEscalationAnswerAsync(id);
        if (!result.Success || result.Data is not { } data)
        {
            Response.StatusCode = (int)result.StatusCode;
            return Json(new { success = false, message = result.Message });
        }

        return Json(new
        {
            success = true,
            draft = data.Draft,
            confidence = data.Confidence switch { "high" => "cao", "medium" => "trung bình", _ => "thấp" },
            missingInformation = data.MissingInformation,
            personalDataRedacted = data.PersonalDataRedacted
        });
    }

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
