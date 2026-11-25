using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>Displays the signed-in student's conversations stored by the API.</summary>
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

    private const int DefaultHistoryPage = 1;

    public async Task<IActionResult> Index(int? conversationId, bool newChat = false, int historyPage = DefaultHistoryPage)
    {
        var status = await _api.GetAssistantStatusAsync();
        var result = await _api.ListAssistantConversationsAsync(Math.Max(DefaultHistoryPage, historyPage));
        if (!result.Success) FlashError(result.Message);
        var history = result.Data ?? new PagedResult<AssistantConversation>();
        AssistantConversationDetail? current = null;
        if (!newChat)
        {
            var id = conversationId ?? history.Items.FirstOrDefault()?.Id;
            if (id.HasValue)
            {
                var detail = await _api.GetAssistantConversationAsync(id.Value);
                if (conversationId.HasValue && detail.StatusCode == System.Net.HttpStatusCode.NotFound) return NotFound();
                if (!detail.Success) FlashError(detail.Message);
                current = detail.Data;
            }
        }

        return View(new AssistantPageViewModel
        {
            Enabled = status.Success && status.Data?.Enabled == true,
            Model = status.Data?.Model,
            StatusMessage = status.Success ? null : status.Message,
            DailyQuestionLimit = status.Data?.DailyQuestionLimit ?? 0,
            RemainingToday = status.Data?.RemainingToday ?? 0,
            Conversations = history,
            Current = current,
            NewChat = newChat
        });
    }

    public static string ToolLabel(string tool) => ToolLabels.GetValueOrDefault(tool, tool);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Chat([FromBody] AssistantChatInput input)
    {
        var result = await _api.AssistantChatAsync(input.ConversationId, input.Message);
        if (!result.Success || result.Data is not { } data)
        {
            Response.StatusCode = (int)result.StatusCode;
            return Json(new { success = false, message = result.Message, errorCode = result.ErrorCode });
        }

        return Json(new
        {
            success = true,
            reply = data.Reply,
            conversationId = data.ConversationId,
            personalDataRedacted = data.PersonalDataRedacted,
            remainingToday = data.RemainingToday,
            toolsUsed = data.ToolsUsed.Select(ToolLabel),
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
