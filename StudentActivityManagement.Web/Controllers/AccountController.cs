using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

[Authorize]
public sealed class AccountController : WebControllerBase
{
    private readonly ApiClient _api;

    public AccountController(ApiClient api) => _api = api;

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _api.LoginAsync(model.UsernameOrEmail.Trim(), model.Password);
        if (!result.Success || result.Data == null)
        {
            AddApiErrors(result);
            return View(model);
        }

        var user = result.Data.User;
        await SignInSessionAsync(result.Data);
        if (user.MustChangePassword)
            return RedirectToAction(nameof(ChangePassword));
        FlashSuccess($"Xin chào {user.FullName}!");
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // US-44: the API revokes every token of the account first, so other browsers and phones are
        // signed out too; the local cookie is dropped whatever the API answers.
        var result = await _api.LogoutAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        FlashSuccess(result.Success ? "Đã đăng xuất khỏi mọi trình duyệt và thiết bị." : "Đã đăng xuất.");
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _api.ForgotPasswordAsync(model.Login);
        if (!result.Success)
        {
            AddApiErrors(result);
            return View(model);
        }
        TempData["PasswordResetLogin"] = model.Login.Trim();
        FlashSuccess(result.Message);
        return RedirectToAction(nameof(ResetPassword));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult ResetPassword() => View(new ResetPasswordViewModel
    {
        Login = TempData["PasswordResetLogin"] as string ?? string.Empty
    });

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _api.ResetPasswordAsync(model.Login, model.Code, model.NewPassword);
        if (!result.Success)
        {
            AddApiErrors(result);
            return View(model);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var result = await _api.GetProfileAsync();
        if (!result.Success) FlashError(result.Message);
        return View(new ProfilePageViewModel
        {
            Details = result.Data, Email = result.Data?.Email ?? string.Empty, Phone = result.Data?.Phone ?? string.Empty
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfilePageViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _api.UpdateContactAsync(model.Email, model.Phone, model.CurrentPassword);
            if (result.Success)
            {
                FlashSuccess(result.Message);
                return RedirectToAction(nameof(Profile));
            }
            AddApiErrors(result);
        }

        model.Details = (await _api.GetProfileAsync()).Data;
        return View(model);
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _api.ChangePasswordAsync(model.CurrentPassword, model.NewPassword);
        if (!result.Success || result.Data == null)
        {
            AddApiErrors(result);
            return View(model);
        }

        await SignInSessionAsync(result.Data);
        FlashSuccess(result.Message);
        return RedirectToAction(nameof(Profile));
    }

    private async Task SignInSessionAsync(AuthResponse session)
    {
        var user = session.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("student_code", user.StudentCode),
            new("campus", user.Campus),
            new("class_code", user.ClassCode ?? string.Empty),
            new(BearerTokenHandler.TokenClaim, session.Token),
        };
        if (user.MustChangePassword)
            claims.Add(new Claim(PasswordChangeRequiredFilter.ClaimType, "1"));
        // The Web session ends with the API token, so an expired token never lingers in the browser.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = session.ExpiresAt });

    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied() => View();
}
