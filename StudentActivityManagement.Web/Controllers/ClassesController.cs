using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-18: Phòng QL HSSV creates classes and appoints the class monitor.</summary>
[Authorize(Policy = WebPolicies.Staff)]
public sealed class ClassesController : WebControllerBase
{
    // A class is small enough to show every student on one page.
    private const int ClassPageSize = 100;
    private readonly ApiClient _api;

    public ClassesController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetClassesAsync();
        if (!result.Success)
        {
            FlashError(result.Message);
        }

        return View(new ClassListViewModel { Classes = result.Data ?? [] });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(ClassListViewModel.NewClass))] ClassCreateForm form)
    {
        if (ModelState.IsValid)
        {
            var result = await _api.CreateClassAsync(form.ClassCode.Trim(), form.ClassName.Trim(), form.Department?.Trim() ?? string.Empty);
            if (result.Success)
            {
                FlashSuccess(result.Message);
                return RedirectToAction(nameof(Index));
            }

            AddApiErrors(result);
        }

        var classes = await _api.GetClassesAsync();
        return View(nameof(Index), new ClassListViewModel { Classes = classes.Data ?? [], NewClass = form });
    }

    public async Task<IActionResult> Details(int id)
    {
        var classes = await _api.GetClassesAsync();
        var current = classes.Data?.FirstOrDefault(item => item.Id == id);
        if (current == null)
        {
            FlashError(classes.Success ? "Không tìm thấy lớp." : classes.Message);
            return RedirectToAction(nameof(Index));
        }

        var students = await _api.GetStudentsAsync(null, null, null, id, 1, ClassPageSize);
        return View(new ClassDetailViewModel { Class = current, Students = students.Data?.Items ?? [] });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignMonitor(int id, int studentId)
    {
        var result = await _api.AssignMonitorAsync(id, studentId);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Details), new { id });
    }
}
