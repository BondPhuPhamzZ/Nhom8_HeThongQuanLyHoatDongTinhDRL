using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Account;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

/// <summary>
/// Quản lý tài khoản và phân quyền (US-25). Chỉ Quản trị viên hệ thống (SystemAdmin) được truy cập.
/// </summary>
[ApiController]
[Route("api/v1/admin/accounts")]
[Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
public sealed class AccountsController : ApiControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    /// Danh sách tài khoản (phân trang, tìm theo mã/tên/email, lọc theo vai trò và trạng thái)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<AccountResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _accountService.GetAccountsAsync(
            search, role, isActive, pageIndex, pageSize, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>
    /// Chi tiết một tài khoản
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<AccountResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccountById(int id)
    {
        var result = await _accountService.GetAccountByIdAsync(id, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>
    /// Tạo tài khoản cán bộ (HSSV hoặc SystemAdmin)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<AccountResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAccount([FromBody] AccountCreateDto request)
    {
        var result = await _accountService.CreateAccountAsync(request, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return ProblemFrom(result);
        }

        return CreatedAtAction(nameof(GetAccountById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Đổi vai trò giữa HSSV và SystemAdmin
    /// </summary>
    [HttpPatch("{id:int}/role")]
    [ProducesResponseType(typeof(ApiResponseDto<AccountResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] AccountRoleUpdateDto request)
    {
        if (!TryGetActorId(out var actorId))
        {
            return InvalidSession();
        }

        var result = await _accountService.UpdateRoleAsync(actorId, id, request.Role, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    /// <summary>
    /// Khóa hoặc mở khóa tài khoản
    /// </summary>
    [HttpPatch("{id:int}/active-status")]
    [ProducesResponseType(typeof(ApiResponseDto<AccountResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateActiveStatus(int id, [FromBody] AccountActiveStatusUpdateDto request)
    {
        if (!TryGetActorId(out var actorId))
        {
            return InvalidSession();
        }

        var result = await _accountService.UpdateActiveStatusAsync(
            actorId, id, request.IsActive!.Value, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    private bool TryGetActorId(out int actorId) =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId);

    private ObjectResult InvalidSession() => ApiProblem(
        StatusCodes.Status401Unauthorized,
        ApiErrorCodes.AuthenticationRequired,
        "Phiên đăng nhập không hợp lệ.");
}
