using System.Data;
using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Account;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

/// <summary>
/// Quản lý tài khoản và phân quyền (US-25). Mọi thao tác thay đổi role hoặc trạng thái đều chạy trong
/// transaction Serializable để không thể làm hệ thống mất Quản trị viên hệ thống đang hoạt động cuối cùng.
/// </summary>
public sealed class AccountService : IAccountService
{
    private readonly AppDbContext _context;

    public AccountService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponseDto<PagedResultDto<AccountResponseDto>>> GetAccountsAsync(
        string? search, string? role, bool? isActive, int pageIndex = 1, int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        string? normalizedRole = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            normalizedRole = Role.Normalize(role);
            if (normalizedRole == null)
            {
                return ApiResponseDto<PagedResultDto<AccountResponseDto>>.Fail(
                    InvalidRoleFilterMessage,
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed);
            }
        }

        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Users.AsNoTracking();
        if (normalizedRole != null)
        {
            query = query.Where(user => user.Role == normalizedRole);
        }

        if (isActive.HasValue)
        {
            query = query.Where(user => user.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(user =>
                user.StudentCode.ToLower().Contains(keyword) ||
                user.FullName.ToLower().Contains(keyword) ||
                user.Email.ToLower().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new AccountResponseDto
            {
                Id = user.Id,
                StudentCode = user.StudentCode,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                IsActive = user.IsActive,
                Campus = user.Campus,
                AcademicYear = user.AcademicYear,
                ClassId = user.ClassId,
                ClassCode = user.Class != null ? user.Class.ClassCode : null,
                IsClassMonitor = user.IsClassMonitor,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResponseDto<PagedResultDto<AccountResponseDto>>.Ok(new PagedResultDto<AccountResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponseDto<AccountResponseDto>> GetAccountByIdAsync(
        int id, CancellationToken cancellationToken = default)
    {
        var account = await _context.Users.AsNoTracking()
            .Include(user => user.Class)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

        return account == null ? NotFound() : ApiResponseDto<AccountResponseDto>.Ok(Map(account));
    }

    public async Task<ApiResponseDto<AccountResponseDto>> CreateAccountAsync(
        AccountCreateDto request, CancellationToken cancellationToken = default)
    {
        var role = Role.Normalize(request.Role);
        if (role == null || !Role.StaffRoles.Contains(role))
        {
            return Invalid(StaffRoleOnlyMessage);
        }

        var studentCode = request.StudentCode.Trim();
        var email = request.Email.Trim();
        var fullName = request.FullName.Trim();
        if (studentCode.Length == 0 || fullName.Length == 0)
        {
            return Invalid("Mã đăng nhập và họ tên không được chỉ chứa khoảng trắng.");
        }

        if (await _context.Users.AnyAsync(user => user.StudentCode == studentCode, cancellationToken))
        {
            return Conflict("Mã đăng nhập đã tồn tại.");
        }

        if (await _context.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Conflict("Email đã được sử dụng.");
        }

        var account = new User
        {
            StudentCode = studentCode,
            FullName = fullName,
            Email = email,
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            Phone = request.Phone?.Trim() ?? string.Empty,
            Role = role,
            Campus = string.IsNullOrWhiteSpace(request.Campus) ? "Cơ sở 1" : request.Campus.Trim(),
            AcademicYear = "Staff",
            ClassId = null,
            IsClassMonitor = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(account);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai request tạo cùng mã/email gần như đồng thời: unique index chặn bản ghi thứ hai.
            return Conflict("Mã đăng nhập hoặc email đã tồn tại.");
        }

        return ApiResponseDto<AccountResponseDto>.Ok(Map(account), "Tạo tài khoản thành công.");
    }

    public async Task<ApiResponseDto<AccountResponseDto>> UpdateRoleAsync(
        int actorId, int targetId, string role, CancellationToken cancellationToken = default)
    {
        var newRole = Role.Normalize(role);
        if (newRole == null || !Role.StaffRoles.Contains(newRole))
        {
            return Invalid(StaffRoleOnlyMessage + " Lớp trưởng được chỉ định qua API quản lý lớp.");
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var target = await _context.Users
            .Include(user => user.Class)
            .FirstOrDefaultAsync(user => user.Id == targetId, cancellationToken);
        if (target == null)
        {
            return NotFound();
        }

        if (target.Id == actorId)
        {
            return BusinessRule("Không thể tự thay đổi vai trò của chính mình.");
        }

        if (!await IsActiveSystemAdminAsync(actorId, cancellationToken))
        {
            return Forbidden();
        }

        if (!Role.StaffRoles.Contains(target.Role))
        {
            return BusinessRule(
                "Chỉ đổi vai trò giữa HSSV và SystemAdmin. Tài khoản sinh viên/lớp trưởng được quản lý qua API sinh viên và lớp.");
        }

        if (target.Role == newRole)
        {
            await transaction.CommitAsync(cancellationToken);
            return ApiResponseDto<AccountResponseDto>.Ok(Map(target), "Vai trò không thay đổi.");
        }

        if (target.Role == Role.SystemAdmin && target.IsActive &&
            !await HasOtherActiveSystemAdminAsync(target.Id, cancellationToken))
        {
            return BusinessRule(LastSystemAdminMessage);
        }

        target.Role = newRole;
        target.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApiResponseDto<AccountResponseDto>.Ok(Map(target), $"Đã cập nhật vai trò thành '{newRole}'.");
    }

    public async Task<ApiResponseDto<AccountResponseDto>> UpdateActiveStatusAsync(
        int actorId, int targetId, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var target = await _context.Users
            .Include(user => user.Class)
            .FirstOrDefaultAsync(user => user.Id == targetId, cancellationToken);
        if (target == null)
        {
            return NotFound();
        }

        if (target.Id == actorId)
        {
            return BusinessRule("Không thể tự khóa hoặc mở khóa tài khoản của chính mình.");
        }

        // Đọc lại actor trong transaction: actor có thể vừa bị một SystemAdmin khác khóa.
        if (!await IsActiveSystemAdminAsync(actorId, cancellationToken))
        {
            return Forbidden();
        }

        if (target.IsActive == isActive)
        {
            await transaction.CommitAsync(cancellationToken);
            return ApiResponseDto<AccountResponseDto>.Ok(Map(target), "Trạng thái tài khoản không thay đổi.");
        }

        if (!isActive && target.Role == Role.SystemAdmin &&
            !await HasOtherActiveSystemAdminAsync(target.Id, cancellationToken))
        {
            return BusinessRule(LastSystemAdminMessage);
        }

        target.IsActive = isActive;
        target.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApiResponseDto<AccountResponseDto>.Ok(
            Map(target),
            isActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.");
    }

    private Task<bool> IsActiveSystemAdminAsync(int userId, CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(
            user => user.Id == userId && user.IsActive && user.Role == Role.SystemAdmin,
            cancellationToken);

    private Task<bool> HasOtherActiveSystemAdminAsync(int excludedUserId, CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(
            user => user.Id != excludedUserId && user.IsActive && user.Role == Role.SystemAdmin,
            cancellationToken);

    private static AccountResponseDto Map(User user) => new()
    {
        Id = user.Id,
        StudentCode = user.StudentCode,
        FullName = user.FullName,
        Email = user.Email,
        Phone = user.Phone,
        Role = user.Role,
        IsActive = user.IsActive,
        Campus = user.Campus,
        AcademicYear = user.AcademicYear,
        ClassId = user.ClassId,
        ClassCode = user.Class?.ClassCode,
        IsClassMonitor = user.IsClassMonitor,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };

    private const string InvalidRoleFilterMessage =
        "Vai trò lọc chỉ nhận Student, Monitor, HSSV hoặc SystemAdmin.";

    private const string StaffRoleOnlyMessage = "Vai trò chỉ nhận HSSV hoặc SystemAdmin.";

    private const string LastSystemAdminMessage =
        "Hệ thống phải còn ít nhất một Quản trị viên hệ thống đang hoạt động.";

    private static ApiResponseDto<AccountResponseDto> NotFound() =>
        ApiResponseDto<AccountResponseDto>.Fail(
            "Không tìm thấy tài khoản.",
            StatusCodes.Status404NotFound,
            ApiErrorCodes.ResourceNotFound);

    private static ApiResponseDto<AccountResponseDto> Invalid(string message) =>
        ApiResponseDto<AccountResponseDto>.Fail(
            message,
            StatusCodes.Status400BadRequest,
            ApiErrorCodes.ValidationFailed);

    private static ApiResponseDto<AccountResponseDto> Conflict(string message) =>
        ApiResponseDto<AccountResponseDto>.Fail(
            message,
            StatusCodes.Status409Conflict,
            ApiErrorCodes.ResourceConflict);

    private static ApiResponseDto<AccountResponseDto> BusinessRule(string message) =>
        ApiResponseDto<AccountResponseDto>.Fail(
            message,
            StatusCodes.Status409Conflict,
            ApiErrorCodes.BusinessRuleViolation);

    private static ApiResponseDto<AccountResponseDto> Forbidden() =>
        ApiResponseDto<AccountResponseDto>.Fail(
            "Tài khoản thực hiện không còn quyền Quản trị viên hệ thống.",
            StatusCodes.Status403Forbidden,
            ApiErrorCodes.AccessDenied);
}
