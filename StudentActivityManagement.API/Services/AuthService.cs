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
                return ApiResponseDto<AuthResponseDto>.Fail(
                    "Tên đăng nhập / Mã sinh viên hoặc mật khẩu không chính xác.",
                    StatusCodes.Status401Unauthorized,
                    ApiErrorCodes.InvalidCredentials);
            }

            if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return ApiResponseDto<AuthResponseDto>.Fail(
                    "Tên đăng nhập / Mã sinh viên hoặc mật khẩu không chính xác.",
                    StatusCodes.Status401Unauthorized,
                    ApiErrorCodes.InvalidCredentials);
            }

            // Kiểm tra sau mật khẩu để không tiết lộ trạng thái tài khoản cho người không biết mật khẩu.
            if (!user.IsActive)
            {
                return ApiResponseDto<AuthResponseDto>.Fail(
                    "Tài khoản đã bị khóa. Vui lòng liên hệ Phòng Quản lý HSSV.",
                    StatusCodes.Status403Forbidden,
                    ApiErrorCodes.AccountDisabled);
            }

            if (PasswordHasher.NeedsRehash(user.PasswordHash))
            {
                user.PasswordHash = PasswordHasher.HashPassword(request.Password);
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
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

        public async Task<ApiResponseDto<UserDto>> GetCurrentUserAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Class)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return ApiResponseDto<UserDto>.Fail(
                    "Không tìm thấy thông tin người dùng.",
                    StatusCodes.Status404NotFound,
                    ApiErrorCodes.ResourceNotFound);
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
