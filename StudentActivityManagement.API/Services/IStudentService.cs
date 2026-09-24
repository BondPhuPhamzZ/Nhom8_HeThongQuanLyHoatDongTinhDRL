using StudentActivityManagement.API.DTOs.Class;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Student;

namespace StudentActivityManagement.API.Services
{
    public interface IStudentService
    {
        Task<ApiResponseDto<PagedResultDto<StudentResponseDto>>> GetStudentsAsync(
            string? campus, string? academicYear, int? classId, string? search, int pageIndex = 1, int pageSize = 10);

        Task<ApiResponseDto<StudentResponseDto>> GetStudentByIdAsync(int id);
        Task<ApiResponseDto<StudentResponseDto>> CreateStudentAsync(StudentCreateDto request);
        Task<ApiResponseDto<StudentResponseDto>> UpdateStudentAsync(int id, StudentUpdateDto request);
        Task<ApiResponseDto<bool>> DeleteStudentAsync(int id);

        // Class management
        Task<ApiResponseDto<List<ClassDto>>> GetClassesAsync();
        Task<ApiResponseDto<ClassDto>> CreateClassAsync(ClassCreateDto request);
        Task<ApiResponseDto<bool>> AssignClassMonitorAsync(int classId, int studentId);
    }
}

