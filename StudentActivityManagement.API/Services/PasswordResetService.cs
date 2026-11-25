using System.Data;
using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Auth;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services.Email;

namespace StudentActivityManagement.API.Services;

public sealed class PasswordResetService(AppDbContext context, TimeProvider time, IEmailSender email,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private const int MaxAttempts = 5;
    private const int MaxCodesPerHour = 3;
    private const string Sent = "Nếu tài khoản tồn tại và đang hoạt động, mã xác nhận đã được gửi tới email đã đăng ký.";
    private const string Unavailable = "Chức năng quên mật khẩu chưa được bật. Vui lòng liên hệ Phòng QL HSSV.";

    public async Task<ApiResponseDto<object>> RequestCodeAsync(ForgotPasswordDto request, string? ip, CancellationToken ct)
    {
        if (!email.IsAvailable)
            return ApiResponseDto<object>.Fail(Unavailable, StatusCodes.Status503ServiceUnavailable, ApiErrorCodes.ServiceUnavailable);

        var now = time.GetUtcNow().UtcDateTime;
        var normal = ApiResponseDto<object>.Ok(new object(), Sent);
        User? user;
        string code;
        await using (var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            user = await FindActiveAsync(request.Login, ct);
            if (user == null) return normal;
            var recentCount = await context.PasswordResetCodes.CountAsync(item =>
                item.UserId == user.Id && item.CreatedAt > now.AddHours(-1), ct);
            if (recentCount >= MaxCodesPerHour) return normal;

            code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            await InvalidateCodesAsync(context, user.Id, now, ct);
            context.PasswordResetCodes.Add(new PasswordResetCode
            {
                UserId = user.Id, CodeHash = Convert.ToHexString(Hash(code)), CreatedAt = now,
                ExpiresAt = now.AddMinutes(10), RequestedIp = ip
            });
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        try
        {
            await email.SendAsync(user.Email, "Mã đặt lại mật khẩu",
                $"Mã đặt lại mật khẩu của bạn là {code}, hiệu lực 10 phút. Nếu bạn không yêu cầu, hãy bỏ qua email này.", ct);
        }
        catch (SmtpException exception)
        {
            logger.LogWarning("Password reset mail could not be sent for account {UserId}: {SmtpStatus}", user.Id, exception.StatusCode);
        }
        return normal;
    }

    public async Task<ApiResponseDto<object>> ResetAsync(ResetPasswordDto request, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await FindActiveAsync(request.Login, ct);
        if (user == null) return InvalidCode();
        var reset = await context.PasswordResetCodes.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(item => item.UserId == user.Id && item.UsedAt == null && item.ExpiresAt > now
                && item.Attempts < MaxAttempts, ct);
        if (reset == null) return InvalidCode();

        if (!CryptographicOperations.FixedTimeEquals(Hash(request.Code.Trim()), Convert.FromHexString(reset.CodeHash)))
        {
            reset.Attempts++;
            if (reset.Attempts >= MaxAttempts) reset.UsedAt = now;
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return InvalidCode();
        }

        var validation = PasswordPolicy.Validate(request.NewPassword, user);
        if (validation != null) return Invalid(validation);
        ApplyPassword(user, request.NewPassword, false, now);
        reset.UsedAt = now;
        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ApiResponseDto<object>.Ok(new object(), "Đã đặt lại mật khẩu. Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    public async Task<ApiResponseDto<TemporaryPasswordDto>> IssueTemporaryAsync(int userId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await context.Users.SingleOrDefaultAsync(item => item.Id == userId, ct);
        if (user == null) return ApiResponseDto<TemporaryPasswordDto>.Fail("Không tìm thấy tài khoản.",
            StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);

        string password;
        do { password = GenerateTemporaryPassword(); } while (PasswordPolicy.Validate(password, user) != null);
        ApplyPassword(user, password, true, now);
        await InvalidateCodesAsync(context, user.Id, now, ct);
        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ApiResponseDto<TemporaryPasswordDto>.Ok(new TemporaryPasswordDto(password), "Đã cấp mật khẩu tạm.");
    }

    internal static Task InvalidateCodesAsync(AppDbContext db, int userId, DateTime now, CancellationToken ct) =>
        db.PasswordResetCodes.Where(item => item.UserId == userId && item.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), ct);

    private Task<User?> FindActiveAsync(string login, CancellationToken ct)
    {
        var key = login.Trim().ToLowerInvariant();
        return context.Users.FirstOrDefaultAsync(item => item.IsActive &&
            (item.StudentCode.ToLower() == key || item.Email.ToLower() == key), ct);
    }

    private static void ApplyPassword(User user, string password, bool temporary, DateTime now)
    {
        user.PasswordHash = PasswordHasher.HashPassword(password);
        user.TokenVersion++;
        user.MustChangePassword = temporary;
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.UpdatedAt = now;
    }

    private static byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));

    private static ApiResponseDto<object> InvalidCode() => Invalid("Mã xác nhận không đúng hoặc đã hết hạn.");

    private static ApiResponseDto<object> Invalid(string message) =>
        ApiResponseDto<object>.Fail(message, StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed);

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;
        Span<char> password = stackalloc char[10];
        password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < password.Length; i++) password[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        for (var i = password.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }
        return new string(password);
    }
}
