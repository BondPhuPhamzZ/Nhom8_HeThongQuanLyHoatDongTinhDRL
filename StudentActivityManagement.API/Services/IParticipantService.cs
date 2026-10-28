using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Participants;

namespace StudentActivityManagement.API.Services;

/// <summary>
/// US-21: Phòng QL HSSV checks who registered and attended, records or undoes attendance by hand,
/// and reviews participation evidence.
/// </summary>
public interface IParticipantService
{
    Task<ApiResponseDto<ActivityParticipantsDto>> GetParticipantsAsync(
        int activityId, string? status, string? search, bool? hasPendingEvidence, int pageIndex, int pageSize);

    Task<ApiResponseDto<ParticipantDetailDto>> GetParticipantAsync(int activityId, int registrationId);

    Task<ApiResponseDto<ParticipantDetailDto>> RecordManualCheckInAsync(int activityId, int sessionId, int registrationId, int staffId);

    Task<ApiResponseDto<ParticipantDetailDto>> RemoveCheckInAsync(int activityId, int sessionId, int registrationId);

    Task<ApiResponseDto<StaffEvidenceDto>> ReviewEvidenceAsync(int activityId, int evidenceId, string status, string? note, int staffId);

    /// <summary>Opens an evidence file for download; null data means there is no file to send.</summary>
    Task<ApiResponseDto<EvidenceFileDownload>> OpenEvidenceFileAsync(int activityId, int evidenceId);
}

public sealed record EvidenceFileDownload(Stream Content, string ContentType, string FileName);
