using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Class;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Student;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services
{
    public class StudentService : IStudentService
    {
        private readonly AppDbContext _context;

        public StudentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponseDto<PagedResultDto<StudentResponseDto>>> GetStudentsAsync(
            string? campus, string? academicYear, int? classId, string? search, int pageIndex = 1, int pageSize = 10)
        {
            var query = _context.Users
                .Include(u => u.Class)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(campus))
            {
                query = query.Where(u => u.Campus == campus);
            }

            if (!string.IsNullOrWhiteSpace(academicYear))
            {
                query = query.Where(u => u.AcademicYear == academicYear);
            }

            if (classId.HasValue)
            {
                query = query.Where(u => u.ClassId == classId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(keyword) || u.StudentCode.ToLower().Contains(keyword) || u.Email.ToLower().Contains(keyword));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(u => MapToStudentResponse(u))
                .ToListAsync();

            var pagedResult = new PagedResultDto<StudentResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };

            return ApiResponseDto<PagedResultDto<StudentResponseDto>>.Ok(pagedResult);
        }

        public async Task<ApiResponseDto<StudentResponseDto>> GetStudentByIdAsync(int id)
        {
            var student = await _context.Users
                .Include(u => u.Class)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (student == null)
            {
                return ApiResponseDto<StudentResponseDto>.Fail("KhÃ´ng tÃ¬m tháº¥y sinh viÃªn.");
            }

            return ApiResponseDto<StudentResponseDto>.Ok(MapToStudentResponse(student));
        }

        public async Task<ApiResponseDto<StudentResponseDto>> CreateStudentAsync(StudentCreateDto request)
        {
            if (await _context.Users.AnyAsync(u => u.StudentCode == request.StudentCode))
            {
                return ApiResponseDto<StudentResponseDto>.Fail("MÃ£ sinh viÃªn Ä‘Ã£ tá»“n táº¡i.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Email Ä‘Ã£ tá»“n táº¡i.");
            }

            var student = new User
            {
                StudentCode = request.StudentCode,
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = PasswordHasher.HashPassword(request.Password),
                Phone = request.Phone,
                Role = string.IsNullOrWhiteSpace(request.Role) ? Role.Student : request.Role,
                Campus = request.Campus,
                AcademicYear = request.AcademicYear,
                ClassId = request.ClassId,
                IsClassMonitor = request.IsClassMonitor,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(student);
            await _context.SaveChangesAsync();

            await _context.Entry(student).Reference(u => u.Class).LoadAsync();

            return ApiResponseDto<StudentResponseDto>.Ok(MapToStudentResponse(student), "ThÃªm sinh viÃªn má»›i thÃ nh cÃ´ng.");
        }

        public async Task<ApiResponseDto<StudentResponseDto>> UpdateStudentAsync(int id, StudentUpdateDto request)
        {
            var student = await _context.Users
                .Include(u => u.Class)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (student == null)
            {
                return ApiResponseDto<StudentResponseDto>.Fail("KhÃ´ng tÃ¬m tháº¥y sinh viÃªn.");
            }

            if (student.Email != request.Email && await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Email Ä‘Ã£ Ä‘Æ°á»£c sá»­ dá»¥ng bá»Ÿi tÃ i khoáº£n khÃ¡c.");
            }

            student.FullName = request.FullName;
            student.Email = request.Email;
            student.Phone = request.Phone;
            student.Role = request.Role;
            student.Campus = request.Campus;
            student.AcademicYear = request.AcademicYear;
            student.ClassId = request.ClassId;
            student.IsClassMonitor = request.IsClassMonitor;
            student.AccumulatedPoints = request.AccumulatedPoints;
            student.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                student.PasswordHash = PasswordHasher.HashPassword(request.Password);
            }

            await _context.SaveChangesAsync();

            return ApiResponseDto<StudentResponseDto>.Ok(MapToStudentResponse(student), "Cáº­p nháº­t sinh viÃªn thÃ nh cÃ´ng.");
        }

        public async Task<ApiResponseDto<bool>> DeleteStudentAsync(int id)
        {
            var student = await _context.Users.FindAsync(id);
            if (student == null)
            {
                return ApiResponseDto<bool>.Fail("KhÃ´ng tÃ¬m tháº¥y sinh viÃªn.");
            }

            _context.Users.Remove(student);
            await _context.SaveChangesAsync();

            return ApiResponseDto<bool>.Ok(true, "XÃ³a sinh viÃªn thÃ nh cÃ´ng.");
        }

        public async Task<ApiResponseDto<List<ClassDto>>> GetClassesAsync()
        {
            var classes = await _context.Classes
                .Include(c => c.MonitorStudent)
                .Include(c => c.Students)
                .Select(c => new ClassDto
                {
                    Id = c.Id,
                    ClassCode = c.ClassCode,
                    ClassName = c.ClassName,
                    Department = c.Department,
                    MonitorStudentId = c.MonitorStudentId,
                    MonitorStudentName = c.MonitorStudent != null ? c.MonitorStudent.FullName : null,
                    TotalStudents = c.Students.Count
                })
                .ToListAsync();

            return ApiResponseDto<List<ClassDto>>.Ok(classes);
        }

        public async Task<ApiResponseDto<ClassDto>> CreateClassAsync(ClassCreateDto request)
        {
            if (await _context.Classes.AnyAsync(c => c.ClassCode == request.ClassCode))
            {
                return ApiResponseDto<ClassDto>.Fail("MÃ£ lá»›p Ä‘Ã£ tá»“n táº¡i.");
            }

            var newClass = new Class
            {
                ClassCode = request.ClassCode,
                ClassName = request.ClassName,
                Department = request.Department,
                MonitorStudentId = request.MonitorStudentId
            };

            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();

            var classDto = new ClassDto
            {
                Id = newClass.Id,
                ClassCode = newClass.ClassCode,
                ClassName = newClass.ClassName,
                Department = newClass.Department,
                MonitorStudentId = newClass.MonitorStudentId,
                TotalStudents = 0
            };

            return ApiResponseDto<ClassDto>.Ok(classDto, "ThÃªm lá»›p má»›i thÃ nh cÃ´ng.");
        }

        private static StudentResponseDto MapToStudentResponse(User user)
        {
            return new StudentResponseDto
            {
                Id = user.Id,
                StudentCode = user.StudentCode,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                Campus = user.Campus,
                AcademicYear = user.AcademicYear,
                ClassId = user.ClassId,
                ClassCode = user.Class?.ClassCode,
                ClassName = user.Class?.ClassName,
                IsClassMonitor = user.IsClassMonitor,
                AccumulatedPoints = user.AccumulatedPoints,
                CreatedAt = user.CreatedAt
            };
        }
        public async Task<ApiResponseDto<bool>> AssignClassMonitorAsync(int classId, int studentId)
        {
            var targetClass = await _context.Classes.FindAsync(classId);
            if (targetClass == null) return ApiResponseDto<bool>.Fail("Không tìm thấy lớp học.");
            
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId && u.Role == Role.Student);
            if (student == null) return ApiResponseDto<bool>.Fail("Không tìm thấy sinh viên.");
            
            if (student.ClassId != classId) return ApiResponseDto<bool>.Fail("Sinh viên này không thuộc lớp.");
            
            // Xoá chức lớp trưởng của người cũ
            if (targetClass.MonitorStudentId.HasValue) {
                var oldMonitor = await _context.Users.FindAsync(targetClass.MonitorStudentId.Value);
                if (oldMonitor != null) oldMonitor.IsClassMonitor = false;
            }
            
            targetClass.MonitorStudentId = studentId;
            student.IsClassMonitor = true;
            
            await _context.SaveChangesAsync();
            return ApiResponseDto<bool>.Ok(true, "Chỉ định lớp trưởng thành công.");
        }
    }
}

