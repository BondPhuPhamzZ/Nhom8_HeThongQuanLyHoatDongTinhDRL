using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.Services.Assistant;

namespace StudentActivityManagement.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
[Route("api/v1/admin/assistant-usage")]
public sealed class AssistantUsageController(IAssistantUsageService service) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("Thống kê số lượt và token AI, tối đa 31 ngày")]
    public async Task<IActionResult> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? keyword)
    {
        var result = await service.ReportAsync(from, to, keyword, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : ProblemFrom(result);
    }
}
