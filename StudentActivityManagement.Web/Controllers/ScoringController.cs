using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-22: confirm results of a completed activity and record training points.</summary>
[Authorize(Policy = WebPolicies.Staff)]
public sealed class ScoringController : WebControllerBase
{
    private readonly ApiClient _api;

    public ScoringController(ApiClient api) => _api = api;

    /// <summary>/Scoring/Index/{id}: scoring sheet of activity {id}.</summary>
    public async Task<IActionResult> Index(int id)
    {
        var result = await _api.GetScoringAsync(id);
        if (!result.Success || result.Data == null)
        {
            FlashError(result.Message);
            return RedirectToAction("Details", "ManageActivities", new { id });
        }

        return View(result.Data);
    }

    /// <summary>Approves the ticked registrations, or every eligible one when <paramref name="all"/> is set.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, List<int>? registrationIds, bool all, string? note)
    {
        if (!all && (registrationIds == null || registrationIds.Count == 0))
        {
            FlashError("Chọn ít nhất một sinh viên để ghi điểm.");
            return RedirectToAction(nameof(Index), new { id });
        }

        var result = await _api.ApprovePointsAsync(id, all ? null : registrationIds, note);
        if (!result.Success)
        {
            FlashError(result.Message);
        }
        else if (result.Data?.Skipped.Count > 0)
        {
            FlashSuccess($"{result.Message} Bỏ qua {result.Data.Skipped.Count} đăng ký: {string.Join("; ", result.Data.Skipped.Select(item => item.Reason).Distinct())}");
        }
        else
        {
            FlashSuccess(result.Message);
        }

        return RedirectToAction(nameof(Index), new { id });
    }

    public async Task<IActionResult> History(int id, int pointRecordId, string? student)
    {
        var result = await _api.GetPointHistoryAsync(pointRecordId);
        if (!result.Success)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Index), new { id });
        }

        return View(new PointHistoryViewModel { ActivityId = id, StudentLabel = student ?? string.Empty, Entries = result.Data ?? [] });
    }
}
