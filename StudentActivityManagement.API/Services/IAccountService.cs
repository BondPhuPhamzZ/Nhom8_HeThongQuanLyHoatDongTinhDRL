using StudentActivityManagement.API.DTOs.Account;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services;

public interface IAccountService
{
    Task<ApiResponseDto<PagedResultDto<AccountResponseDto>>> GetAccountsAsync(
        string? search, string? role, bool? isActive, int pageIndex = 1, int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AccountResponseDto>> GetAccountByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AccountResponseDto>> CreateAccountAsync(
        AccountCreateDto request, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AccountResponseDto>> UpdateRoleAsync(
        int actorId, int targetId, string role, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AccountResponseDto>> UpdateActiveStatusAsync(
        int actorId, int targetId, bool isActive, CancellationToken cancellationToken = default);
}
