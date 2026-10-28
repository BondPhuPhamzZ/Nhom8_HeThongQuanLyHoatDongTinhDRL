using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-17: Phòng QL HSSV manages student records by academic year, class and campus.</summary>
[Authorize(Policy = WebPolicies.Staff)]
public sealed class StudentsController : WebControllerBase
{
    private readonly ApiClient _api;

    public StudentsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(string? search, string? campus, string? academicYear, int? classId, int page = 1)
    {
        var result = await _api.GetStudentsAsync(search, campus, academicYear, classId, Math.Max(page, 1));
        if (!result.Success)
        {
            FlashError(result.Message);
        }

        return View(new StudentListViewModel
        {
            Page = result.Data ?? new(),
            Classes = (await _api.GetClassesAsync()).Data ?? [],
            Search = search,
            Campus = campus,
            AcademicYear = academicYear,
            ClassId = classId
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? classId)
    {
        var model = new StudentFormViewModel { ClassId = classId };
        await LoadClassesAsync(model);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Nhập mật khẩu ban đầu cho sinh viên.");
        }
        if (!ModelState.IsValid)
        {
            await LoadClassesAsync(model);
            return View("Form", model);
        }

        var result = await _api.CreateStudentAsync(model.ToRequest());
        if (!result.Success)
        {
            AddApiErrors(result);
            await LoadClassesAsync(model);
            return View("Form", model);
        }

        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Index), new { search = result.Data?.StudentCode });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _api.GetStudentAsync(id);
        if (!result.Success || result.Data == null)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Index));
        }

        var model = StudentFormViewModel.From(result.Data);
        await LoadClassesAsync(model);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StudentFormViewModel model)
    {
        model.Id = id;
        // The code is fixed after creation; it is shown read-only and not validated again.
        ModelState.Remove(nameof(model.StudentCode));
        if (!ModelState.IsValid)
        {
            await LoadClassesAsync(model);
            return View("Form", model);
        }

        var result = await _api.UpdateStudentAsync(id, model.ToRequest());
        if (!result.Success)
        {
            AddApiErrors(result);
            await LoadClassesAsync(model);
            return View("Form", model);
        }

        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Index), new { search = result.Data?.StudentCode });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _api.DeleteStudentAsync(id);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadClassesAsync(StudentFormViewModel model) =>
        model.Classes = (await _api.GetClassesAsync()).Data ?? [];
}
