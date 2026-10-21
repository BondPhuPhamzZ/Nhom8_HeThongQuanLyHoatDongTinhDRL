using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Controllers;

[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult ProblemFrom<T>(ApiResponseDto<T> result)
    {
        return ApiProblem(result.HttpStatusCode, result.ErrorCode, result.Message);
    }

    protected ObjectResult ApiProblem(int statusCode, string errorCode, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Yêu cầu không hợp lệ",
        StatusCodes.Status401Unauthorized => "Chưa xác thực",
        StatusCodes.Status403Forbidden => "Không có quyền truy cập",
        StatusCodes.Status404NotFound => "Không tìm thấy dữ liệu",
        StatusCodes.Status409Conflict => "Xung đột dữ liệu",
        _ => "Có lỗi xảy ra"
    };
}
