using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;

namespace StudentActivityManagement.Web.ViewComponents;

/// <summary>
/// Check-in block on the student activity page (US-07): window and status per session, code form when open.
/// Rendered only for students holding a registration; loads its own data from the API.
/// </summary>
public sealed class StudentCheckInViewComponent : ViewComponent
{
    private readonly ApiClient _api;

    public StudentCheckInViewComponent(ApiClient api) => _api = api;

    public async Task<IViewComponentResult> InvokeAsync(int activityId, string? registrationStatus)
    {
        if (registrationStatus is null or "Cancelled")
        {
            return Content(string.Empty);
        }

        var result = await LoadAsync(() => _api.GetCheckInStatusAsync(activityId));
        return result?.Data == null ? Content(string.Empty) : View(result.Data);
    }

    // A 401 while rendering a view cannot be turned into a redirect anymore; the next action request will do it.
    internal static async Task<ApiResult<T>?> LoadAsync<T>(Func<Task<ApiResult<T>>> call)
    {
        try
        {
            return await call();
        }
        catch (ApiUnauthorizedException)
        {
            return null;
        }
    }
}

/// <summary>Session check-in codes on the staff activity page (US-07): issue or rotate a code, see how many checked in.</summary>
public sealed class CheckInCodesViewComponent : ViewComponent
{
    private readonly ApiClient _api;

    public CheckInCodesViewComponent(ApiClient api) => _api = api;

    public async Task<IViewComponentResult> InvokeAsync(int activityId)
    {
        var result = await StudentCheckInViewComponent.LoadAsync(() => _api.GetCheckInCodesAsync(activityId));
        return result?.Data == null ? Content(string.Empty) : View(result.Data);
    }
}
