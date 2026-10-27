using System.Security.Claims;

namespace StudentActivityManagement.API.Services;

/// <summary>
/// Kiểm tra token đã phát vẫn còn hiệu lực theo dữ liệu tài khoản hiện tại.
/// </summary>
public interface IAccountAccessValidator
{
    /// <summary>
    /// Trả về false nếu tài khoản không còn tồn tại, đã bị khóa hoặc vai trò đã thay đổi so với token.
    /// </summary>
    Task<bool> IsTokenStillValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
