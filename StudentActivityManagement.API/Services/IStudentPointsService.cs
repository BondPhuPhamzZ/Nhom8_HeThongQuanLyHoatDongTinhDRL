using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Student;

namespace StudentActivityManagement.API.Services;

public interface IStudentPointsService
{
    Task<ApiResponseDto<StudentPointsRecommendationDto>> GetPointsAndRecommendationsAsync(int studentId, int? periodId);
}
