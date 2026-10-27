using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Infrastructure;

namespace StudentActivityManagement.Web.ViewComponents;

/// <summary>Navbar block with the signed-in user's name, code and role, read from the auth cookie (no API call).</summary>
public sealed class UserPanelViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var user = HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        return View(new UserPanelModel(
            user.Identity.Name ?? string.Empty,
            user.FindFirstValue("student_code") ?? string.Empty,
            WebPolicies.RoleLabel(user.FindFirstValue(ClaimTypes.Role)),
            user.FindFirstValue("class_code")));
    }
}

public sealed record UserPanelModel(string FullName, string Code, string RoleLabel, string? ClassCode);
