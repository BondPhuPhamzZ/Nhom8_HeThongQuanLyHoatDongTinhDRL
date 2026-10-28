using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;
using StudentActivityManagement.Web.Models;

namespace StudentActivityManagement.Web.Controllers;

/// <summary>US-21: Phòng QL HSSV checks who registered and attended, records attendance by hand and reviews evidence.</summary>
[Authorize(Policy = WebPolicies.Staff)]
public sealed class ParticipantsController : WebControllerBase
{
    private readonly ApiClient _api;

    public ParticipantsController(ApiClient api) => _api = api;

    /// <summary>/Participants/Index/{id}: participants of activity {id}.</summary>
    public async Task<IActionResult> Index(int id, string? status, string? search, bool pending = false, int page = 1)
    {
        var result = await _api.GetParticipantsAsync(id, status, search, pending, Math.Max(page, 1));
        if (!result.Success || result.Data == null)
        {
            FlashError(result.Message);
            return RedirectToAction("Details", "ManageActivities", new { id });
        }

        return View(new ParticipantListViewModel { Data = result.Data, Status = status, Search = search, PendingOnly = pending });
    }

    /// <summary>/Participants/Details/{id}?registrationId=…: one participant with attendance per session and evidence.</summary>
    public async Task<IActionResult> Details(int id, int registrationId)
    {
        var participant = await _api.GetParticipantAsync(id, registrationId);
        var activity = await _api.GetActivityAsync(id);
        if (!participant.Success || participant.Data == null || activity.Data == null)
        {
            FlashError(participant.Success ? activity.Message : participant.Message);
            return RedirectToAction(nameof(Index), new { id });
        }

        return View(new ParticipantDetailViewModel { Activity = activity.Data, Participant = participant.Data });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordCheckIn(int id, int sessionId, int registrationId, string? returnTo)
    {
        var result = await _api.RecordCheckInAsync(id, sessionId, registrationId);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return BackTo(id, registrationId, returnTo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCheckIn(int id, int sessionId, int registrationId, string? returnTo)
    {
        var result = await _api.RemoveCheckInAsync(id, sessionId, registrationId);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return BackTo(id, registrationId, returnTo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewEvidence(int id, int registrationId, int evidenceId, string status, string? note)
    {
        var result = await _api.ReviewEvidenceAsync(id, evidenceId, status, note);
        if (result.Success) FlashSuccess(result.Message); else FlashError(result.Message);
        return RedirectToAction(nameof(Details), new { id, registrationId });
    }

    /// <summary>Streams an evidence file through the Web, because the browser has no API token.</summary>
    public async Task<IActionResult> EvidenceFile(int id, int evidenceId, int registrationId)
    {
        var result = await _api.GetEvidenceFileAsync(id, evidenceId);
        if (!result.Success || result.Data == null)
        {
            FlashError(result.Message);
            return RedirectToAction(nameof(Details), new { id, registrationId });
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    // The list keeps its own filters in returnTo; anything that is not a local URL falls back to the participant page.
    private IActionResult BackTo(int id, int registrationId, string? returnTo) =>
        !string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo)
            ? LocalRedirect(returnTo)
            : RedirectToAction(nameof(Details), new { id, registrationId });
}
