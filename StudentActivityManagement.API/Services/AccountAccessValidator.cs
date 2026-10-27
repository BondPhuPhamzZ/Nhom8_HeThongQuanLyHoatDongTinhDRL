using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;

namespace StudentActivityManagement.API.Services;

/// <summary>
/// Đối chiếu JWT với database ở mỗi request để khóa tài khoản và đổi quyền có hiệu lực ngay,
/// thay vì chờ token hết hạn.
/// </summary>
public sealed class AccountAccessValidator : IAccountAccessValidator
{
    private readonly AppDbContext _context;

    public AccountAccessValidator(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsTokenStillValidAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            return false;
        }

        var tokenRole = principal.FindFirst(ClaimTypes.Role)?.Value;
        var account = await _context.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.IsActive, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        return account != null &&
               account.IsActive &&
               string.Equals(account.Role, tokenRole, StringComparison.Ordinal);
    }
}
