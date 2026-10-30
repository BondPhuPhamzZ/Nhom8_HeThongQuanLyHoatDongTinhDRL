using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;

namespace StudentActivityManagement.Web.Controllers;

[Authorize(Policy = WebPolicies.Student)]
public sealed class NotificationsController : WebControllerBase
{
    private readonly ApiClient _api;

    public NotificationsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(bool unreadOnly = false, int page = 1)
    {
        var result = await _api.GetNotificationsAsync(unreadOnly, Math.Max(page, 1));
        if (!result.Success) FlashError(result.Message);
        ViewBag.UnreadOnly = unreadOnly;
        return View(result.Data ?? new PagedResult<UserNotification>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(int id, bool unreadOnly = false, int page = 1)
    {
        var result = await _api.MarkNotificationReadAsync(id);
        if (!result.Success) FlashError(result.Message);
        return RedirectToAction(nameof(Index), new { unreadOnly, page });
    }
}
