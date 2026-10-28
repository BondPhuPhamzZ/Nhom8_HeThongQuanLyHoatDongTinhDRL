using Microsoft.AspNetCore.Authentication.Cookies;
using StudentActivityManagement.Web.Api;
using StudentActivityManagement.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// The Web app never touches the database: every feature goes through the shared RESTful API.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5210";

builder.Services.AddControllersWithViews(options => options.Filters.Add<ApiUnauthorizedFilter>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<ApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

// The API's JWT is kept as a claim inside the auth cookie, which ASP.NET Core encrypts with Data Protection.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "sam.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(WebPolicies.Student, policy => policy.RequireRole("Student", "Monitor"));
    options.AddPolicy(WebPolicies.Staff, policy => policy.RequireRole("HSSV", "SystemAdmin"));
    options.AddPolicy(WebPolicies.SystemAdmin, policy => policy.RequireRole("SystemAdmin"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
