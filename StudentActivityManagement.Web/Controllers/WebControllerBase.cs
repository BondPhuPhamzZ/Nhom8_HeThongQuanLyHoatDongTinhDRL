using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;

namespace StudentActivityManagement.Web.Controllers;

public abstract class WebControllerBase : Controller
{
    protected void FlashSuccess(string message) => TempData[Flash.Success] = message;

    protected void FlashError(string message) => TempData[Flash.Error] = message;

    /// <summary>Puts the API's message and field errors on the form so the user sees what to fix.</summary>
    protected void AddApiErrors<T>(ApiResult<T> result)
    {
        ModelState.AddModelError(string.Empty, result.Message);
        foreach (var (field, messages) in result.Errors)
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(string.Empty, message);
            }
        }
    }
}
