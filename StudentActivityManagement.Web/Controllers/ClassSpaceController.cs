using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-09: the student's own class discussion space (questions and discussions between classmates).</summary>
[Authorize(Policy = WebPolicies.Student)]
public sealed class ClassSpaceController : WebControllerBase
{
    private readonly ApiClient _api;

    public ClassSpaceController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(string? kind, string? search, int page = 1) =>
        View(await LoadIndexAsync(kind, search, page, new ClassThreadForm()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(ClassSpaceViewModel.NewThread))] ClassThreadForm form)
    {
        if (ModelState.IsValid)
        {
            var result = await _api.CreateClassThreadAsync(form.Kind, form.Title, form.Body);
            if (result.Success && result.Data != null)
            {
                FlashSuccess(result.Message);
                return RedirectToAction(nameof(Thread), new { id = result.Data.Id });
            }
            AddApiErrors(result);
        }

        return View(nameof(Index), await LoadIndexAsync(null, null, 1, form));
    }

    public async Task<IActionResult> Thread(int id)
    {
        var result = await _api.GetClassThreadAsync(id);
        if (!result.Success || result.Data == null)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int id, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            FlashError("Nhập nội dung trả lời.");
            return RedirectToAction(nameof(Thread), new { id });
        }

        var result = await _api.ReplyClassThreadAsync(id, body);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Thread), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _api.DeleteClassThreadAsync(id);
        if (!result.Success)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Thread), new { id });
        }

        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReply(int id, int replyId)
    {
        var result = await _api.DeleteClassThreadReplyAsync(id, replyId);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Thread), new { id });
    }

    /// <summary>US-15: the class monitor raises the thread to Phòng QL HSSV.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Escalate(int id, string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            FlashError("Nhập nội dung cần Phòng QL HSSV xác nhận.");
            return RedirectToAction(nameof(Thread), new { id });
        }

        var result = await _api.EscalateClassThreadAsync(id, question);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Thread), new { id });
    }

    /// <summary>US-15: questions the monitor has raised for the class, with their status and any official answer.</summary>
    public async Task<IActionResult> Escalations(string? status, int page = 1)
    {
        var result = await _api.GetClassEscalationsAsync(status, Math.Max(page, 1));
        if (!result.Success)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Status = status;
        return View(result.Data ?? new PagedResult<ThreadEscalation>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int escalationId, string? status)
    {
        var result = await _api.WithdrawClassEscalationAsync(escalationId);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Escalations), new { status });
    }

    private async Task<ClassSpaceViewModel> LoadIndexAsync(string? kind, string? search, int page, ClassThreadForm form)
    {
        var result = await _api.GetClassSpaceAsync(kind, search, Math.Max(page, 1));
        if (!result.Success)
        {
            // 409 means the student has no class yet; the view explains that instead of an error banner.
            ViewBag.Notice = result.Message;
        }

        return new ClassSpaceViewModel { Space = result.Data, Kind = kind, Search = search, NewThread = form };
    }
}
