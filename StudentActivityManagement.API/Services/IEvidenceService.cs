using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Evidence;

namespace StudentActivityManagement.API.Services;

public interface IEvidenceService
{
    Task<ApiResponseDto<List<EvidenceResponseDto>>> GetEvidencesAsync(int activityId, int studentId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<EvidenceResponseDto>> UploadAsync(int activityId, int studentId, EvidenceUploadDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<EvidenceFileDownload>> OpenFileAsync(int activityId, int evidenceId, int studentId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<object>> DeleteAsync(int activityId, int evidenceId, int studentId, CancellationToken cancellationToken = default);
}
