using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace StudentActivityManagement.Web.Infrastructure;

public sealed class PasswordChangeRequiredFilter : IActionFilter
{
    public const string ClaimType = "pwd_change";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.HttpContext.User.HasClaim(ClaimType, "1")) return;
        var route = context.ActionDescriptor.RouteValues;
        if (route.TryGetValue("controller", out var controller) && controller == "Account" &&
            route.TryGetValue("action", out var action) && action is "ChangePassword" or "Logout")
            return;
        context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
