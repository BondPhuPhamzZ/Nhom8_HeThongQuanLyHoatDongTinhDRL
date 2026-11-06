using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-31: chat page of the training-point assistant. The conversation stays in the browser; this controller only relays it.</summary>
[Authorize(Policy = WebPolicies.Student)]
public sealed class AssistantController : WebControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> ToolLabels = new Dictionary<string, string>
    {
        ["get_points_summary"] = "điểm và mục tiêu của kỳ",
        ["list_evaluation_periods"] = "các kỳ đánh giá",
        ["get_my_activity_history"] = "lịch sử hoạt động của bạn",
        ["search_activities"] = "hoạt động phù hợp",
        ["get_activity_detail"] = "chi tiết hoạt động"
    };

    private static readonly IReadOnlyDictionary<string, string> RegistrationLabels = new Dictionary<string, string>
    {
        ["Registered"] = "Đã đăng ký",
        ["Attended"] = "Đã tham gia",
        ["Cancelled"] = "Đã hủy",
        ["Absent"] = "Vắng mặt"
    };

    private readonly ApiClient _api;

    public AssistantController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var status = await _api.GetAssistantStatusAsync();
        return View(new AssistantPageViewModel
        {
            Enabled = status.Success && status.Data?.Enabled == true,
            Model = status.Data?.Model,
            StatusMessage = status.Success ? null : status.Message
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Chat([FromBody] AssistantChatInput input)
    {
        var result = await _api.AssistantChatAsync(input.Messages ?? []);
        if (!result.Success || result.Data is not { } data)
        {
            Response.StatusCode = (int)result.StatusCode;
            return Json(new { success = false, message = result.Message, errorCode = result.ErrorCode });
        }

        return Json(new
        {
            success = true,
            reply = data.Reply,
            toolsUsed = data.ToolsUsed.Select(tool => ToolLabels.GetValueOrDefault(tool, tool)),
            activities = data.Activities.Select(activity => new
            {
                id = activity.Id,
                name = activity.ActivityName,
                points = activity.TrainingPoints,
                campus = activity.Campus,
                start = activity.StartTime is { } start ? VnTime.Format(start) : null,
                registrationClose = activity.RegistrationCloseTime is { } close ? VnTime.Format(close) : null,
                isRegistrationOpen = activity.IsRegistrationOpen,
                registration = activity.MyRegistrationStatus is { } status ? RegistrationLabels.GetValueOrDefault(status, status) : null,
                url = Url.Action("Details", "StudentActivities", new { id = activity.Id })
            })
        });
    }
}
