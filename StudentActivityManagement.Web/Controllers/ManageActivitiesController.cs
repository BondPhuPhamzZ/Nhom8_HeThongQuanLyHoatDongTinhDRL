using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>Phòng QL HSSV / SystemAdmin: list, create, edit, publish/cancel and delete activities.</summary>
[Authorize(Policy = WebPolicies.Staff)]
public sealed class ManageActivitiesController : WebControllerBase
{
    private readonly ApiClient _api;

    public ManageActivitiesController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        var result = await _api.GetActivitiesAsync(search, status, Math.Max(page, 1));
        if (!result.Success)
        {
            FlashError(result.Message);
        }

        return View(new ActivityListViewModel { Page = result.Data ?? new(), Search = search, Status = status });
    }

    public async Task<IActionResult> Details(int id)
    {
        var result = await _api.GetActivityAsync(id);
        if (!result.Success || result.Data == null)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = ActivityFormViewModel.NewDefault();
        await LoadClassesAsync(model);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityFormViewModel model)
    {
        if (!await ValidateFormAsync(model))
        {
            return View("Form", model);
        }

        var result = await _api.CreateActivityAsync(model.ToRequest());
        if (!result.Success || result.Data == null)
        {
            AddApiErrors(result);
            await LoadClassesAsync(model);
            return View("Form", model);
        }

        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Details), new { id = result.Data.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _api.GetActivityAsync(id);
        if (!result.Success || result.Data == null)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Index));
        }

        var model = ActivityFormViewModel.From(result.Data);
        await LoadClassesAsync(model);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityFormViewModel model)
    {
        model.Id = id;
        if (!await ValidateFormAsync(model))
        {
            return View("Form", model);
        }

        var result = await _api.UpdateActivityAsync(id, model.ToRequest());
        if (!result.Success)
        {
            AddApiErrors(result);
            await LoadClassesAsync(model);
            return View("Form", model);
        }

        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string status)
    {
        var result = await _api.UpdateActivityStatusAsync(id, status);
        if (result.Success) FlashSuccess($"Đã chuyển hoạt động sang trạng thái \"{StatusLabel(status)}\"."); else FlashError(result.Message);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateCheckInCode(int id, int sessionId)
    {
        var result = await _api.GenerateCheckInCodeAsync(id, sessionId);
        if (result.Success) FlashSuccess($"{result.Message} Mã mới: {result.Data?.Code}"); else FlashError(result.Message);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _api.DeleteActivityAsync(id);
        if (!result.Success)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Details), new { id });
        }

        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ValidateFormAsync(ActivityFormViewModel model)
    {
        if (model.Sessions.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Hoạt động phải có ít nhất một buổi.");
        }

        if (ModelState.IsValid)
        {
            return true;
        }

        await LoadClassesAsync(model);
        return false;
    }

    private static string StatusLabel(string status) => status switch
    {
        "Draft" => "Nháp",
        "Published" => "Đang mở",
        "Ongoing" => "Đang diễn ra",
        "Completed" => "Đã kết thúc",
        "Cancelled" => "Đã hủy",
        _ => status
    };

    private async Task LoadClassesAsync(ActivityFormViewModel model)
    {
        var classes = await _api.GetClassesAsync();
        model.Classes = classes.Data ?? [];
    }
}
