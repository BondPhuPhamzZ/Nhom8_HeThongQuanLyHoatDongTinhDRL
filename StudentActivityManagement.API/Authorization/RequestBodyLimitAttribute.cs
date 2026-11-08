using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Authorization;

/// <summary>
/// Rejects a request body larger than <see cref="MaxBytes"/> with 413 before model binding reads it (US-32).
/// A declared Content-Length is checked at once; for chunked bodies the server's body-size limit is lowered,
/// so Kestrel stops reading as soon as the limit is passed.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequestBodyLimitAttribute : Attribute, IResourceFilter, IOrderedFilter
{
    public RequestBodyLimitAttribute(long maxBytes) => MaxBytes = maxBytes;

    public long MaxBytes { get; }

    // Run before the framework's body-reading filters.
    public int Order => int.MinValue;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var http = context.HttpContext;
        if (http.Request.ContentLength > MaxBytes)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Dữ liệu gửi lên quá lớn",
                Detail = $"Nội dung gửi lên tối đa {MaxBytes / 1024} KB.",
                Type = "https://httpstatuses.com/413",
                Instance = http.Request.Path
            };
            problem.Extensions["errorCode"] = ApiErrorCodes.PayloadTooLarge;
            problem.Extensions["traceId"] = http.TraceIdentifier;
            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge,
                ContentTypes = { "application/problem+json" }
            };
            return;
        }

        var sizeFeature = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = MaxBytes;
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
