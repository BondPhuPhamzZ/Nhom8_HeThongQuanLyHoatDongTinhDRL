using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Auth;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Profile;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed partial class ProfileService(AppDbContext context, IJwtTokenGenerator jwt, TimeProvider time) : IProfileService
{
    public async Task<ApiResponseDto<ProfileDto>> GetAsync(int userId, CancellationToken ct)
    {
        var user = await context.Users.AsNoTracking().Include(item => item.Class)
            .SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, ct);
        return user == null ? Missing<ProfileDto>() : ApiResponseDto<ProfileDto>.Ok(Map(user));
    }

    public async Task<ApiResponseDto<ProfileDto>> UpdateAsync(int userId, UpdateContactDto request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = request.Phone.Trim();
        if (email.Length is 0 or > 150 || !new EmailAddressAttribute().IsValid(email))
            return Invalid<ProfileDto>("Email không hợp lệ hoặc dài quá 150 ký tự.");
        if (!PhonePattern().IsMatch(phone)) return Invalid<ProfileDto>("Số điện thoại không đúng định dạng Việt Nam.");

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await ActiveUserAsync(userId, ct);
        if (user == null) return Missing<ProfileDto>();
        if (!PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash)) return WrongPassword<ProfileDto>();
        if (await context.Users.AnyAsync(item => item.Id != userId && item.Email.ToLower() == email, ct))
            return ApiResponseDto<ProfileDto>.Fail("Email đã được sử dụng.", StatusCodes.Status409Conflict, ApiErrorCodes.ResourceConflict);

        var now = time.GetUtcNow().UtcDateTime;
        user.Email = email;
        user.Phone = phone;
        user.UpdatedAt = now;
        context.UserNotifications.Add(new UserNotification
        {
            RecipientUserId = userId,
            SourceKey = $"profile-updated:{userId}:{Guid.NewGuid():N}",
            Kind = "ProfileUpdated",
            Title = "Đã cập nhật hồ sơ",
            Body = "Thông tin liên hệ của bạn vừa được cập nhật.",
            CreatedAt = now
        });
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApiResponseDto<ProfileDto>.Ok(Map(user), "Đã cập nhật thông tin liên hệ.");
    }

    public async Task<ApiResponseDto<AuthResponseDto>> ChangePasswordAsync(int userId, ChangePasswordDto request, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await ActiveUserAsync(userId, ct);
        if (user == null) return Missing<AuthResponseDto>();
        if (!PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash)) return WrongPassword<AuthResponseDto>();
        var validation = PasswordPolicy.Validate(request.NewPassword, user);
        if (validation != null) return Invalid<AuthResponseDto>(validation);

        user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        user.TokenVersion++;
        user.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApiResponseDto<AuthResponseDto>.Ok(AuthService.CreateSession(user, jwt),
            "Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.");
    }

    private Task<User?> ActiveUserAsync(int userId, CancellationToken ct) => context.Users.Include(item => item.Class)
        .SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, ct);

    private static ProfileDto Map(User user) => new(user.StudentCode, user.FullName, user.Role, user.Class?.ClassCode,
        user.Campus, user.AcademicYear, user.Email, user.Phone, user.UpdatedAt);

    private static ApiResponseDto<T> Missing<T>() =>
        ApiResponseDto<T>.Fail("Không tìm thấy người dùng.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);

    private static ApiResponseDto<T> WrongPassword<T>() =>
        ApiResponseDto<T>.Fail("Mật khẩu hiện tại không đúng.", StatusCodes.Status400BadRequest, ApiErrorCodes.InvalidCredentials);

    private static ApiResponseDto<T> Invalid<T>(string message) =>
        ApiResponseDto<T>.Fail(message, StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);

    [GeneratedRegex(@"^(0|\+84)(3|5|7|8|9)\d{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
