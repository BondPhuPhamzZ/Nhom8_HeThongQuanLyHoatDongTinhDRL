using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentActivityManagement.API.Data;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// US-25: quản lý tài khoản, phân quyền và hiệu lực của khóa tài khoản/đổi vai trò.
/// Không khóa hoặc đổi vai trò các tài khoản seed trong class này.
/// </summary>
public sealed class AccountManagementTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string AccountsUrl = "/api/v1/admin/accounts";

    private readonly CustomWebApplicationFactory _factory;
    private readonly AccountTestClient _api;

    public AccountManagementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task AccountEndpoints_WithoutToken_Return401()
    {
        var response = await _api.GetAsync(AccountsUrl, null);

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "authentication_required");
    }

    [Theory]
    [InlineData("SV210002", "123456")]
    [InlineData("SV210001", "123456")]
    [InlineData(AccountTestClient.HssvCode, AccountTestClient.HssvPassword)]
    public async Task AccountEndpoints_ForNonSystemAdminRoles_Return403(string code, string password)
    {
        var token = await _api.LoginAsync(code, password);

        var response = await _api.GetAsync(AccountsUrl, token);

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.Forbidden, "access_denied");
    }

    [Fact]
    public async Task Hssv_CanUseBusinessAdminEndpoints_ButNotStudentEndpoints()
    {
        var token = await _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/admin/students", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/admin/classes", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/admin/activities", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync("/api/v1/student-activities", token)).StatusCode);
    }

    [Fact]
    public async Task ListAccounts_FiltersBySearchRoleAndStatus_AndNeverExposesPasswordHash()
    {
        var admin = await LoginSystemAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        await _api.CreateStaffAccountAsync(admin, "HSSV", tag);
        var second = await _api.CreateStaffAccountAsync(admin, "HSSV", tag);
        var lockResponse = await _api.PatchActiveStatusAsync(admin, second.GetProperty("id").GetInt32(), false);
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);

        var allResponse = await _api.GetAsync($"{AccountsUrl}?search={tag}&role=hssv", admin);
        var activeResponse = await _api.GetAsync($"{AccountsUrl}?search={tag}&role=HSSV&isActive=true", admin);
        var lockedResponse = await _api.GetAsync($"{AccountsUrl}?search={tag}&isActive=false", admin);
        var studentResponse = await _api.GetAsync($"{AccountsUrl}?role=Student&pageSize=100", admin);

        Assert.Equal(2, await TotalCountAsync(allResponse));
        Assert.Equal(1, await TotalCountAsync(activeResponse));
        Assert.Equal(1, await TotalCountAsync(lockedResponse));
        var students = (await AccountTestClient.ReadJsonAsync(studentResponse))
            .GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(students);
        Assert.All(students, item => Assert.Equal("Student", item.GetProperty("role").GetString()));

        var fullList = await (await _api.GetAsync($"{AccountsUrl}?pageSize=100", admin)).Content.ReadAsStringAsync();
        var detail = await (await _api.GetAsync($"{AccountsUrl}/{second.GetProperty("id").GetInt32()}", admin))
            .Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordhash", fullList, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordhash", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PBKDF2", fullList, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("abc")]
    public async Task ListAccounts_WithUnknownRoleFilter_Returns400(string role)
    {
        var admin = await LoginSystemAdminAsync();

        var response = await _api.GetAsync($"{AccountsUrl}?role={role}", admin);

        await AccountTestClient.AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task CreateAccount_HappyPath_Returns201AndAccountCanLogin()
    {
        var admin = await LoginSystemAdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var code = $"CB{suffix}";

        var response = await _api.SendAsync(HttpMethod.Post, AccountsUrl, admin, new
        {
            studentCode = code,
            fullName = "Cán bộ Phòng HSSV",
            email = $"cb{suffix}@school.edu.vn",
            password = AccountTestClient.StaffPassword,
            role = "HSSV"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var data = (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal("HSSV", data.GetProperty("role").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
        Assert.Equal("Staff", data.GetProperty("academicYear").GetString());

        var login = await _api.LoginResponseAsync(code, AccountTestClient.StaffPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var user = (await AccountTestClient.ReadJsonAsync(login)).GetProperty("data").GetProperty("user");
        Assert.Equal("HSSV", user.GetProperty("role").GetString());
    }

    [Fact]
    public async Task CreateAccount_DuplicateOrInvalidInput_ReturnsExpectedErrors()
    {
        var admin = await LoginSystemAdminAsync();
        var existing = await _api.CreateStaffAccountAsync(admin, "HSSV");
        var suffix = Guid.NewGuid().ToString("N")[..10];

        var duplicateCode = await CreateRawAsync(admin, existing.GetProperty("studentCode").GetString()!,
            $"new{suffix}@school.edu.vn", AccountTestClient.StaffPassword, "HSSV");
        var duplicateEmail = await CreateRawAsync(admin, $"NEW{suffix}",
            existing.GetProperty("email").GetString()!, AccountTestClient.StaffPassword, "HSSV");
        var studentRole = await CreateRawAsync(admin, $"S{suffix}", $"s{suffix}@school.edu.vn",
            AccountTestClient.StaffPassword, "Student");
        var monitorRole = await CreateRawAsync(admin, $"M{suffix}", $"m{suffix}@school.edu.vn",
            AccountTestClient.StaffPassword, "Monitor");
        var legacyRole = await CreateRawAsync(admin, $"A{suffix}", $"a{suffix}@school.edu.vn",
            AccountTestClient.StaffPassword, "Admin");
        var shortPassword = await CreateRawAsync(admin, $"P{suffix}", $"p{suffix}@school.edu.vn",
            "1234567", "HSSV");

        await AccountTestClient.AssertProblemAsync(duplicateCode, HttpStatusCode.Conflict, "resource_conflict");
        await AccountTestClient.AssertProblemAsync(duplicateEmail, HttpStatusCode.Conflict, "resource_conflict");
        await AccountTestClient.AssertProblemAsync(studentRole, HttpStatusCode.BadRequest, "validation_failed");
        await AccountTestClient.AssertProblemAsync(monitorRole, HttpStatusCode.BadRequest, "validation_failed");
        await AccountTestClient.AssertProblemAsync(legacyRole, HttpStatusCode.BadRequest, "validation_failed");
        await AccountTestClient.AssertProblemAsync(shortPassword, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task UnknownAccount_Returns404()
    {
        var admin = await LoginSystemAdminAsync();
        const int missingId = 987654;

        var detail = await _api.GetAsync($"{AccountsUrl}/{missingId}", admin);
        var role = await _api.PatchRoleAsync(admin, missingId, "HSSV");
        var status = await _api.PatchActiveStatusAsync(admin, missingId, false);

        await AccountTestClient.AssertProblemAsync(detail, HttpStatusCode.NotFound, "resource_not_found");
        await AccountTestClient.AssertProblemAsync(role, HttpStatusCode.NotFound, "resource_not_found");
        await AccountTestClient.AssertProblemAsync(status, HttpStatusCode.NotFound, "resource_not_found");
    }

    [Fact]
    public async Task ChangeRole_TakesEffectImmediately_AndOldTokenIsRejected()
    {
        var admin = await LoginSystemAdminAsync();
        var account = await _api.CreateStaffAccountAsync(admin, "HSSV");
        var code = account.GetProperty("studentCode").GetString()!;
        var oldToken = await _api.LoginAsync(code, AccountTestClient.StaffPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.GetAsync(AccountsUrl, oldToken)).StatusCode);

        var patch = await _api.PatchRoleAsync(admin, account.GetProperty("id").GetInt32(), "SystemAdmin");

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("SystemAdmin",
            (await AccountTestClient.ReadJsonAsync(patch)).GetProperty("data").GetProperty("role").GetString());
        await AccountTestClient.AssertProblemAsync(
            await _api.GetAsync("/api/v1/admin/students", oldToken),
            HttpStatusCode.Unauthorized,
            "authentication_required");

        var newToken = await _api.LoginAsync(code, AccountTestClient.StaffPassword);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync(AccountsUrl, newToken)).StatusCode);

        var unchanged = await _api.PatchRoleAsync(admin, account.GetProperty("id").GetInt32(), "systemadmin");
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_ForStudentAccountsOrToMonitor_IsRejected()
    {
        var admin = await LoginSystemAdminAsync();
        var studentId = await FindAccountIdAsync(admin, "SV210002");
        var staff = await _api.CreateStaffAccountAsync(admin, "HSSV");

        var studentToStaff = await _api.PatchRoleAsync(admin, studentId, "HSSV");
        var toMonitor = await _api.PatchRoleAsync(admin, staff.GetProperty("id").GetInt32(), "Monitor");
        var toStudent = await _api.PatchRoleAsync(admin, staff.GetProperty("id").GetInt32(), "Student");
        var emptyBody = await _api.SendAsync(
            HttpMethod.Patch, $"{AccountsUrl}/{staff.GetProperty("id").GetInt32()}/role", admin, new { });

        await AccountTestClient.AssertProblemAsync(studentToStaff, HttpStatusCode.Conflict, "business_rule_violation");
        await AccountTestClient.AssertProblemAsync(toMonitor, HttpStatusCode.BadRequest, "validation_failed");
        await AccountTestClient.AssertProblemAsync(toStudent, HttpStatusCode.BadRequest, "validation_failed");
        await AccountTestClient.AssertProblemAsync(emptyBody, HttpStatusCode.BadRequest, "validation_failed");

        var student = (await AccountTestClient.ReadJsonAsync(await _api.GetAsync($"{AccountsUrl}/{studentId}", admin)))
            .GetProperty("data");
        Assert.Equal("Student", student.GetProperty("role").GetString());
    }

    [Fact]
    public async Task SystemAdmin_CannotChangeOwnRoleOrLockSelf()
    {
        var admin = await LoginSystemAdminAsync();
        var adminId = await _api.GetCurrentUserIdAsync(admin);

        var demoteSelf = await _api.PatchRoleAsync(admin, adminId, "HSSV");
        var lockSelf = await _api.PatchActiveStatusAsync(admin, adminId, false);

        await AccountTestClient.AssertProblemAsync(demoteSelf, HttpStatusCode.Conflict, "business_rule_violation");
        await AccountTestClient.AssertProblemAsync(lockSelf, HttpStatusCode.Conflict, "business_rule_violation");
        var self = (await AccountTestClient.ReadJsonAsync(await _api.GetAsync($"{AccountsUrl}/{adminId}", admin)))
            .GetProperty("data");
        Assert.Equal("SystemAdmin", self.GetProperty("role").GetString());
        Assert.True(self.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task LockAccount_RejectsExistingTokenAndLogin_UnlockRestoresAccess()
    {
        var admin = await LoginSystemAdminAsync();
        var account = await _api.CreateStaffAccountAsync(admin, "HSSV");
        var accountId = account.GetProperty("id").GetInt32();
        var code = account.GetProperty("studentCode").GetString()!;
        var existingToken = await _api.LoginAsync(code, AccountTestClient.StaffPassword);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/admin/students", existingToken)).StatusCode);

        var lockResponse = await _api.PatchActiveStatusAsync(admin, accountId, false);

        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
        Assert.False((await AccountTestClient.ReadJsonAsync(lockResponse))
            .GetProperty("data").GetProperty("isActive").GetBoolean());
        await AccountTestClient.AssertProblemAsync(
            await _api.GetAsync("/api/v1/admin/students", existingToken),
            HttpStatusCode.Unauthorized,
            "authentication_required");
        await AccountTestClient.AssertProblemAsync(
            await _api.LoginResponseAsync(code, AccountTestClient.StaffPassword),
            HttpStatusCode.Forbidden,
            "account_disabled");
        await AccountTestClient.AssertProblemAsync(
            await _api.LoginResponseAsync(code, "wrong-password"),
            HttpStatusCode.Unauthorized,
            "invalid_credentials");

        var unlockResponse = await _api.PatchActiveStatusAsync(admin, accountId, true);

        Assert.Equal(HttpStatusCode.OK, unlockResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.LoginResponseAsync(code, AccountTestClient.StaffPassword)).StatusCode);
    }

    [Fact]
    public async Task ActiveStatus_IsIdempotent_AndRequiresIsActive()
    {
        var admin = await LoginSystemAdminAsync();
        var account = await _api.CreateStaffAccountAsync(admin, "HSSV");
        var accountId = account.GetProperty("id").GetInt32();

        var first = await _api.PatchActiveStatusAsync(admin, accountId, true);
        var second = await _api.PatchActiveStatusAsync(admin, accountId, true);
        var emptyBody = await _api.SendAsync(
            HttpMethod.Patch, $"{AccountsUrl}/{accountId}/active-status", admin, new { });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await AccountTestClient.AssertProblemAsync(emptyBody, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task OpenApi_DescribesAccountEndpointsAsSecured()
    {
        var response = await _api.GetAsync("/swagger/v1/swagger.json", null);
        var document = await AccountTestClient.ReadJsonAsync(response);
        var paths = document.GetProperty("paths");
        var list = paths.GetProperty("/api/v1/admin/accounts").GetProperty("get");
        var lockOperation = paths.GetProperty("/api/v1/admin/accounts/{id}/active-status").GetProperty("patch");

        Assert.True(list.TryGetProperty("security", out _));
        Assert.True(list.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(lockOperation.GetProperty("responses").TryGetProperty("409", out _));
        Assert.False(paths.TryGetProperty("/api/v1/auth/register", out _));
    }

    [Fact]
    public async Task Startup_MigratesLegacyAdminRoleAndSeedsActiveAccounts()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.Users.AnyAsync(user => user.Role == "Admin"));
        var admin = await db.Users.SingleAsync(user => user.StudentCode == AccountTestClient.SystemAdminCode);
        var hssv = await db.Users.SingleAsync(user => user.StudentCode == AccountTestClient.HssvCode);
        Assert.Equal("SystemAdmin", admin.Role);
        Assert.Equal("HSSV", hssv.Role);
        var seedCodes = new[] { "ADMIN001", "HSSV001", "SV210001", "SV210002" };
        Assert.True(await db.Users.Where(user => seedCodes.Contains(user.StudentCode)).AllAsync(user => user.IsActive));
    }

    [Fact]
    public async Task PublicSelfRegistration_IsRemoved()
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/auth/register", null, new
        {
            studentCode = "SVSELF0001",
            fullName = "Tự đăng ký",
            email = "selfregister@student.edu.vn",
            password = "123456",
            academicYear = "K22",
            classId = 2
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<string> LoginSystemAdminAsync() =>
        _api.LoginAsync(AccountTestClient.SystemAdminCode, AccountTestClient.SystemAdminPassword);

    private Task<HttpResponseMessage> CreateRawAsync(
        string token, string code, string email, string password, string role) =>
        _api.SendAsync(HttpMethod.Post, AccountsUrl, token, new
        {
            studentCode = code,
            fullName = "Tài khoản không hợp lệ",
            email,
            password,
            role
        });

    private async Task<int> FindAccountIdAsync(string token, string code)
    {
        var response = await _api.GetAsync($"{AccountsUrl}?search={code}", token);
        var items = (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("items");
        return items.EnumerateArray()
            .Single(item => item.GetProperty("studentCode").GetString() == code)
            .GetProperty("id").GetInt32();
    }

    private static async Task<int> TotalCountAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await AccountTestClient.ReadJsonAsync(response)).GetProperty("data").GetProperty("totalCount").GetInt32();
    }
}
