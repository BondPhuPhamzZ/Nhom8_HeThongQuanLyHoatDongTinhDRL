using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StudentActivityManagement.API.OpenApi;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .Concat(context.MethodInfo.GetCustomAttributes(true))
            .ToArray() ?? Array.Empty<object>();

        var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any() && !allowsAnonymous;

        AddProblemResponse(operation, context, "500", "Lỗi máy chủ");
        if (!requiresAuthorization)
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            }
        ];

        AddProblemResponse(operation, context, "401", "Chưa xác thực");
        AddProblemResponse(operation, context, "403", "Không có quyền truy cập");
    }

    private static void AddProblemResponse(
        OpenApiOperation operation,
        OperationFilterContext context,
        string statusCode,
        string description)
    {
        if (operation.Responses.ContainsKey(statusCode))
        {
            return;
        }

        operation.Responses[statusCode] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
                }
            }
        };
    }
}
