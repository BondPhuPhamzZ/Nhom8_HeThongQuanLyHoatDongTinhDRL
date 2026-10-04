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
            pageIndex = Math.Max(pageIndex, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.Users
                .Include(u => u.Class)
                .Where(u => u.Role == Role.Student || u.Role == Role.Monitor);

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
                .FirstOrDefaultAsync(u => u.Id == id && (u.Role == Role.Student || u.Role == Role.Monitor));

            if (student == null)
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Không tìm thấy sinh viên.");
            }

            return ApiResponseDto<StudentResponseDto>.Ok(MapToStudentResponse(student));
        }

        public async Task<ApiResponseDto<StudentResponseDto>> CreateStudentAsync(StudentCreateDto request)
        {
            if (await _context.Users.AnyAsync(u => u.StudentCode == request.StudentCode))
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Mã sinh viên đã tồn tại.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Email đã tồn tại.");
            }

            if (request.ClassId.HasValue && !await _context.Classes.AnyAsync(c => c.Id == request.ClassId.Value))
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Lớp học không tồn tại.");
            }

            var student = new User
            {
                StudentCode = request.StudentCode,
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = PasswordHasher.HashPassword(request.Password),
                Phone = request.Phone,
                Role = Role.Student,
                Campus = request.Campus,
                AcademicYear = request.AcademicYear,
                ClassId = request.ClassId,
                IsClassMonitor = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(student);
            await _context.SaveChangesAsync();

            await _context.Entry(student).Reference(u => u.Class).LoadAsync();

            return ApiResponseDto<StudentResponseDto>.Ok(MapToStudentResponse(student), "Thêm sinh viên mới thành công.");
        }

        public async Task<ApiResponseDto<StudentResponseDto>> UpdateStudentAsync(int id, StudentUpdateDto request)
        {
            var student = await _context.Users
                .Include(u => u.Class)
                .FirstOrDefaultAsync(u => u.Id == id && (u.Role == Role.Student || u.Role == Role.Monitor));

            if (student == null)
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Không tìm thấy sinh viên.");
            }

            if (student.Email != request.Email && await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Email đã được sử dụng bởi tài khoản khác.");
            }

            if (request.ClassId.HasValue && !await _context.Classes.AnyAsync(c => c.Id == request.ClassId.Value))
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Lớp học không tồn tại.");
            }

            if (student.IsClassMonitor && student.ClassId != request.ClassId)
            {
                return ApiResponseDto<StudentResponseDto>.Fail("Hãy chỉ định lớp trưởng mới trước khi chuyển lớp cho lớp trưởng hiện tại.");
            }

            student.FullName = request.FullName;
            student.Email = request.Email;
            student.Phone = request.Phone;
            student.Campus = request.Campus;
            student.AcademicYear = request.AcademicYear;
            student.ClassId = request.ClassId;
            student.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                student.PasswordHash = PasswordHasher.HashPassword(request.Password);
            }

            await _context.SaveChangesAsync();

            return ApiResponseDto<StudentResponseDto>.Ok(MapToStudentResponse(student), "Cập nhật sinh viên thành công.");
        }

        public async Task<ApiResponseDto<bool>> DeleteStudentAsync(int id)
        {
            var student = await _context.Users.FirstOrDefaultAsync(u =>
                u.Id == id && (u.Role == Role.Student || u.Role == Role.Monitor));
            if (student == null)
            {
                return ApiResponseDto<bool>.Fail("Không tìm thấy sinh viên.");
            }

            if (await _context.ActivityRegistrations.AnyAsync(registration => registration.StudentId == id))
            {
                return ApiResponseDto<bool>.Fail(
                    "Không thể xóa sinh viên đã có lịch sử đăng ký hoạt động. Hãy khóa tài khoản ở Sprint quản trị tài khoản.");
            }

            _context.Users.Remove(student);
            await _context.SaveChangesAsync();

            return ApiResponseDto<bool>.Ok(true, "Xóa sinh viên thành công.");
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
                return ApiResponseDto<ClassDto>.Fail("Mã lớp đã tồn tại.");
            }

            var newClass = new Class
            {
                ClassCode = request.ClassCode,
                ClassName = request.ClassName,
                Department = request.Department
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

            return ApiResponseDto<ClassDto>.Ok(classDto, "Thêm lớp mới thành công.");
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

            var student = await _context.Users.FirstOrDefaultAsync(u =>
                u.Id == studentId && (u.Role == Role.Student || u.Role == Role.Monitor));
            if (student == null) return ApiResponseDto<bool>.Fail("Không tìm thấy sinh viên.");

            if (student.ClassId != classId) return ApiResponseDto<bool>.Fail("Sinh viên này không thuộc lớp.");

            // Xoá chức lớp trưởng của người cũ
            if (targetClass.MonitorStudentId.HasValue)
            {
                var oldMonitor = await _context.Users.FindAsync(targetClass.MonitorStudentId.Value);
                if (oldMonitor != null)
                {
                    oldMonitor.IsClassMonitor = false;
                    oldMonitor.Role = Role.Student;
                }
            }

            targetClass.MonitorStudentId = studentId;
            student.IsClassMonitor = true;
            student.Role = Role.Monitor;

            await _context.SaveChangesAsync();
            return ApiResponseDto<bool>.Ok(true, "Chỉ định lớp trưởng thành công.");
        }
    }
}

