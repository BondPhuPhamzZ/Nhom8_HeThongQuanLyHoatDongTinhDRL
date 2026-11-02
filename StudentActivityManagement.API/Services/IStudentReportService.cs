using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Student;

namespace StudentActivityManagement.API.Services;

public interface IStudentReportService
{
    Task<ApiResponseDto<List<StudentEvaluationPeriodDto>>> GetPeriodsAsync(int studentId);
    Task<ApiResponseDto<StudentEvaluationHistoryDto>> GetActivityHistoryAsync(int studentId, int? periodId, string? registrationStatus);
}
