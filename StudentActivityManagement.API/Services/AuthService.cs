using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Auth;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;

        public AuthService(AppDbContext context, IJwtTokenGenerator jwtTokenGenerator)
        {
            _context = context;
            _jwtTokenGenerator = jwtTokenGenerator;
        }

        public async Task<ApiResponseDto<AuthResponseDto>> LoginAsync(LoginDto request)
        {
            var user = await _context.Users
                .Include(u => u.Class)
                .FirstOrDefaultAsync(u => u.StudentCode == request.UsernameOrEmail || u.Email == request.UsernameOrEmail);

            if (user == null)
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Tên đăng nhập / Mã sinh viên hoặc mật khẩu không chính xác.");
            }

            if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Tên đăng nhập / Mã sinh viên hoặc mật khẩu không chính xác.");
            }

            var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);

            var userDto = MapToUserDto(user);

            var response = new AuthResponseDto
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = userDto
            };

            return ApiResponseDto<AuthResponseDto>.Ok(response, "Đăng nhập thành công.");
        }

        public async Task<ApiResponseDto<UserDto>> RegisterAsync(RegisterDto request)
        {
            if (await _context.Users.AnyAsync(u => u.StudentCode == request.StudentCode))
            {
                return ApiResponseDto<UserDto>.Fail("Mã sinh viên đã tồn tại trong hệ thống.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return ApiResponseDto<UserDto>.Fail("Email đã được sử dụng.");
            }

            var newUser = new User
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
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            await _context.Entry(newUser).Reference(u => u.Class).LoadAsync();

            return ApiResponseDto<UserDto>.Ok(MapToUserDto(newUser), "Đăng ký tài khoản thành công.");
        }

        public async Task<ApiResponseDto<UserDto>> GetCurrentUserAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Class)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return ApiResponseDto<UserDto>.Fail("Không tìm thấy thông tin người dùng.");
            }

            return ApiResponseDto<UserDto>.Ok(MapToUserDto(user));
        }

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
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
                AccumulatedPoints = user.AccumulatedPoints
            };
        }
    }
}
