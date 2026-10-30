using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.OpenApi;
using StudentActivityManagement.API.Serialization;
using StudentActivityManagement.API.Services;
using StudentActivityManagement.API.Services.Settings;
using StudentActivityManagement.API.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

// Console/Debug logging works consistently in local development, CI and containers.
// Avoid the Windows Event Log provider, which requires machine-level permissions.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 1. Add DbContext
// Read the connection string when the context is resolved, not while the builder runs:
// WebApplicationFactory applies its configuration overrides after this point, so an eager
// read would make every integration test share one persistent database.
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
    options.UseSqlite(
        serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
        ?? "Data Source=student_activity.db"));

// 2. Add Dependency Injection Services
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IActivityEligibilityService, ActivityEligibilityService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountAccessValidator, AccountAccessValidator>();
builder.Services.AddScoped<ICheckInConfigProvider, CheckInConfigProvider>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddScoped<IEvaluationPeriodService, EvaluationPeriodService>();
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IClassSpaceService, ClassSpaceService>();
builder.Services.AddScoped<IStaffEscalationService, StaffEscalationService>();
builder.Services.AddScoped<IEvidenceService, EvidenceService>();

// Evidence files (US-08): stored outside wwwroot, validated by extension, size and signature.
builder.Services.Configure<EvidenceStorageOptions>(builder.Configuration.GetSection(EvidenceStorageOptions.SectionName));
builder.Services.AddSingleton<IEvidenceFileStorage, LocalEvidenceFileStorage>();
builder.Services.AddSingleton<IEvidenceFileValidator, EvidenceFileValidator>();

// 3. Configure JWT Authentication
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "Thiếu JwtSettings:Secret hoặc khóa ngắn hơn 32 ký tự. " +
        "Tại thư mục project, hãy chạy: pwsh -NoProfile -File .\\scripts\\setup-local.ps1. " +
        "Trong CI/Production, hãy cấu hình biến môi trường JwtSettings__Secret.");
}
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "StudentActivityManagement.API";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "StudentActivityClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
    // US-25: token của tài khoản đã bị khóa, bị xóa hoặc đã đổi vai trò bị từ chối ngay (401),
    // client phải đăng nhập lại để nhận token mới.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var validator = context.HttpContext.RequestServices.GetRequiredService<IAccountAccessValidator>();
            if (!await validator.IsTokenStillValidAsync(context.Principal!, context.HttpContext.RequestAborted))
            {
                context.Fail("Tài khoản đã bị khóa hoặc quyền đã thay đổi. Vui lòng đăng nhập lại.");
            }
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.StudentOrMonitor, policy =>
        policy.RequireRole(Role.StudentRoles));
    options.AddPolicy(AuthorizationPolicies.BusinessAdmin, policy =>
        policy.RequireRole(Role.StaffRoles));
    options.AddPolicy(AuthorizationPolicies.SystemAdminOnly, policy =>
        policy.RequireRole(Role.SystemAdmin));
});

// 4. Add Controllers and one error contract for every client (Web/Mobile).
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("errorCode", ApiErrorCodes.UnexpectedError);
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
    };
});
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter()))
    .ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu đầu vào không hợp lệ",
            Type = "https://httpstatuses.com/400",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = ApiErrorCodes.ValidationFailed;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

// 5. Configure Swagger / OpenAPI with JWT Authorization Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hệ thống Quản lý Hoạt động & Tính điểm Rèn luyện API",
        Version = "v1",
        Description = "RESTful API dùng chung cho Web và Mobile của Nhóm 8"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập Token vào ô theo định dạng: Bearer {token_cua_ban}"
    });

    c.OperationFilter<AuthorizeOperationFilter>();
});

// 6. Configure CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApps", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// 7. Apply committed migrations, then seed reference/demo data.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
    DbInitializer.Initialize(context);
    if (app.Environment.IsDevelopment())
    {
        DbInitializer.EnsureDevelopmentAccounts(context);
    }
}

// 8. Configure HTTP Request Pipeline
app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    var request = statusCodeContext.HttpContext.Request;
    var problemDetailsService = statusCodeContext.HttpContext.RequestServices
        .GetRequiredService<IProblemDetailsService>();

    var errorCode = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => ApiErrorCodes.AuthenticationRequired,
        StatusCodes.Status403Forbidden => ApiErrorCodes.AccessDenied,
        StatusCodes.Status404NotFound => ApiErrorCodes.ResourceNotFound,
        _ => ApiErrorCodes.UnexpectedError
    };

    await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
    {
        HttpContext = statusCodeContext.HttpContext,
        ProblemDetails = new ProblemDetails
        {
            Status = response.StatusCode,
            Title = response.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => "Chưa xác thực",
                StatusCodes.Status403Forbidden => "Không có quyền truy cập",
                StatusCodes.Status404NotFound => "Không tìm thấy endpoint",
                _ => "Có lỗi xảy ra"
            },
            Type = $"https://httpstatuses.com/{response.StatusCode}",
            Instance = request.Path,
            Extensions =
            {
                ["errorCode"] = errorCode,
                ["traceId"] = statusCodeContext.HttpContext.TraceIdentifier
            }
        }
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Student Activity Management API v1");
        c.RoutePrefix = string.Empty;
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("ClientApps");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Required by WebApplicationFactory in integration tests.
public partial class Program;
