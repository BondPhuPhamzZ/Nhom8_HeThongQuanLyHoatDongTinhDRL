using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using StudentActivityManagement.Web.Api;

namespace StudentActivityManagement.Web.Infrastructure;

/// <summary>
/// When the API answers 401 (token expired, account locked, role changed), end the Web session too
/// and send the user back to the login page with the reason.
/// </summary>
public sealed class ApiUnauthorizedFilter : IAsyncExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempDataFactory;

    public ApiUnauthorizedFilter(ITempDataDictionaryFactory tempDataFactory) => _tempDataFactory = tempDataFactory;

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiUnauthorizedException exception)
        {
            return;
        }

        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _tempDataFactory.GetTempData(context.HttpContext)[Flash.Error] = exception.Message;
        context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = context.HttpContext.Request.Path.Value });
        context.ExceptionHandled = true;
    }
}

/// <summary>TempData keys for one-shot messages rendered by the _Flash partial.</summary>
public static class Flash
{
    public const string Success = "flash_success";
    public const string Error = "flash_error";
}
