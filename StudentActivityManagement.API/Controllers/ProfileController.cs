using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Profile;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public sealed class ProfileController(IProfileService profile) : ApiControllerBase
{
    [HttpGet("profile")]
    public async Task<IActionResult> Get()
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await profile.GetAsync(userId, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    [HttpPut("profile")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Update([FromBody] UpdateContactDto request)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await profile.UpdateAsync(userId, request, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request)
    {
        if (!TryGetUserId(out var userId)) return InvalidSession();
        var result = await profile.ChangePasswordAsync(userId, request, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private ObjectResult InvalidSession() =>
        ApiProblem(StatusCodes.Status401Unauthorized, ApiErrorCodes.AuthenticationRequired, "Không xác định được tài khoản.");
}
