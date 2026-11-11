using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>Student / class monitor flow: browse eligible activities, view details, register and cancel.</summary>
[Authorize(Policy = WebPolicies.Student)]
public sealed class StudentActivitiesController : WebControllerBase
{
    private readonly ApiClient _api;

    public StudentActivitiesController(ApiClient api) => _api = api;

    /// <param name="mine">True shows the student's registered activities, ended ones included (to see attendance and points).</param>
    public async Task<IActionResult> Index(string? search, string? campus, int page = 1, bool mine = false)
    {
        var result = await _api.GetStudentActivitiesAsync(search, campus, Math.Max(page, 1), registered: mine);
        if (!result.Success)
        {
            FlashError(result.Message);
        }

        return View(new ActivityListViewModel { Page = result.Data ?? new(), Search = search, Campus = campus, Mine = mine });
    }

    public async Task<IActionResult> Details(int id)
    {
        var result = await _api.GetStudentActivityAsync(id);
        if (!result.Success || result.Data == null)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(int id)
    {
        var result = await _api.RegisterAsync(id);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(int id, int sessionId, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            FlashError("Nhập mã check-in do ban tổ chức công bố.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _api.CheckInAsync(id, sessionId, code);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _api.CancelRegistrationAsync(id);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Details), new { id });
    }
}
