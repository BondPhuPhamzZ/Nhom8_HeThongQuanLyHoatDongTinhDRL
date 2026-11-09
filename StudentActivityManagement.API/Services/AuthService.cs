using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Auth;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IOptions<LoginSecurityOptions> _loginSecurity;

        public AuthService(AppDbContext context, IJwtTokenGenerator jwtTokenGenerator,
            IOptions<LoginSecurityOptions> loginSecurity)
        {
            _context = context;
            _jwtTokenGenerator = jwtTokenGenerator;
            _loginSecurity = loginSecurity;
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

            var now = DateTime.UtcNow;
            if (user.LockoutEndUtc > now)
            {
                var minutes = Math.Max(1, (int)Math.Ceiling((user.LockoutEndUtc.Value - now).TotalMinutes));
                return ApiResponseDto<AuthResponseDto>.Fail(
                    $"Tài khoản tạm khóa do đăng nhập sai nhiều lần. Thử lại sau {minutes} phút.",
                    StatusCodes.Status423Locked,
                    ApiErrorCodes.AccountTemporarilyLocked);
            }

            if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                var security = _loginSecurity.Value;
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= Math.Max(1, security.MaxFailedAttempts))
                {
                    user.FailedLoginCount = 0;
                    user.LockoutEndUtc = now.AddMinutes(Math.Max(1, security.LockoutMinutes));
                }
                user.UpdatedAt = now;
                await _context.SaveChangesAsync();
                return ApiResponseDto<AuthResponseDto>.Fail(
                    "Tên đăng nhập / Mã sinh viên hoặc mật khẩu không chính xác.",
                    StatusCodes.Status401Unauthorized,
                    ApiErrorCodes.InvalidCredentials);
            }

            // A valid password clears the temporary lock and the previous failure streak.
            var accountChanged = user.FailedLoginCount != 0 || user.LockoutEndUtc.HasValue;
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;

            // Kiểm tra sau mật khẩu để không tiết lộ trạng thái tài khoản cho người không biết mật khẩu.
            if (!user.IsActive)
            {
                if (accountChanged) await _context.SaveChangesAsync();
                return ApiResponseDto<AuthResponseDto>.Fail(
                    "Tài khoản đã bị khóa. Vui lòng liên hệ Phòng Quản lý HSSV.",
                    StatusCodes.Status403Forbidden,
                    ApiErrorCodes.AccountDisabled);
            }

            if (PasswordHasher.NeedsRehash(user.PasswordHash))
            {
                user.PasswordHash = PasswordHasher.HashPassword(request.Password);
                accountChanged = true;
            }
            if (accountChanged)
            {
                user.UpdatedAt = now;
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
