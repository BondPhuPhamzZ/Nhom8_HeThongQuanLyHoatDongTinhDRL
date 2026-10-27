using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

public sealed class HomeController : Controller
{
    /// <summary>Each role lands on its own starting page.</summary>
    [Authorize]
    public IActionResult Index() => WebPolicies.IsStaff(User)
        ? RedirectToAction("Index", "ManageActivities")
        : RedirectToAction("Index", "StudentActivities");

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
