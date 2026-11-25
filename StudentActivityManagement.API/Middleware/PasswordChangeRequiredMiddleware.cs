using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Middleware;

public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.HasClaim(JwtTokenGenerator.PasswordChangeClaim, "1") || IsAllowed(context.Request))
            return next(context);

        return Results.Problem(
            detail: "Bạn phải đổi mật khẩu tạm trước khi tiếp tục.",
            instance: context.Request.Path,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Cần đổi mật khẩu",
            type: "https://httpstatuses.com/403",
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = ApiErrorCodes.PasswordChangeRequired, ["traceId"] = context.TraceIdentifier
            }).ExecuteAsync(context);
    }

    private static bool IsAllowed(HttpRequest request)
    {
        var path = request.Path.Value?.TrimEnd('/').ToLowerInvariant();
        return (HttpMethods.IsGet(request.Method) && path is "/api/v1/auth/me" or "/api/v1/me/profile")
            || (HttpMethods.IsPost(request.Method) && path is "/api/v1/auth/logout" or "/api/v1/me/change-password");
    }
}
