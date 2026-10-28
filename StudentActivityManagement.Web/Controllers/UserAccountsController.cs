using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>
/// US-25: the system administrator lists all accounts, creates staff accounts, switches staff between
/// HSSV and SystemAdmin, and locks or unlocks accounts. Self-lock, self-demotion and losing the last
/// active SystemAdmin are refused by the API and shown as messages here.
/// </summary>
[Authorize(Policy = WebPolicies.SystemAdmin)]
public sealed class UserAccountsController : WebControllerBase
{
    private readonly ApiClient _api;

    public UserAccountsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(string? search, string? role, string? active, int page = 1)
    {
        bool? isActive = bool.TryParse(active, out var parsed) ? parsed : null;
        var result = await _api.GetAccountsAsync(search, role, isActive, Math.Max(page, 1));
        if (!result.Success)
        {
            FlashError(result.Message);
        }

        return View(new AccountListViewModel
        {
            Page = result.Data ?? new(),
            Search = search,
            Role = role,
            Active = active,
            CurrentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new AccountCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccountCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _api.CreateAccountAsync(model.ToRequest());
        if (!result.Success)
        {
            AddApiErrors(result);
            return View(model);
        }

        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Index), new { search = result.Data?.StudentCode });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, string role, string? returnTo)
    {
        var result = await _api.UpdateAccountRoleAsync(id, role);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return BackTo(returnTo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool isActive, string? returnTo)
    {
        var result = await _api.UpdateAccountActiveAsync(id, isActive);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return BackTo(returnTo);
    }

    private IActionResult BackTo(string? returnTo) =>
        !string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo) ? LocalRedirect(returnTo) : RedirectToAction(nameof(Index));
}
