using System.Net;
using System.Text.Json;

namespace StudentActivityManagement.API.Tests;

/// <summary>
/// US-18 completion: edit and delete classes, remove the monitor. Every test works on classes it creates itself,
/// so the seeded 21DTH1/22DTH2 data other tests rely on stays untouched.
/// </summary>
public sealed class ClassManagementTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "123456";
    private readonly AccountTestClient _api;

    public ClassManagementTests(CustomWebApplicationFactory factory)
    {
        _api = new AccountTestClient(factory.CreateClient());
    }

    [Fact]
    public async Task Update_ChangesClassInfo_AndRejectsDuplicateCode()
    {
        var staff = await StaffAsync();
        var classId = await CreateClassAsync(staff, $"UPD{Suffix()}");

        var newCode = $"NEW{Suffix()}";
        var updated = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/classes/{classId}", staff,
            new { classCode = $"  {newCode} ", className = "Lớp đã đổi tên", department = "Khoa Kinh tế" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var data = await DataAsync(updated);
        Assert.Equal(newCode, data.GetProperty("classCode").GetString());
        Assert.Equal("Lớp đã đổi tên", data.GetProperty("className").GetString());

        var duplicate = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/classes/{classId}", staff,
            new { classCode = "21DTH1", className = "Trùng mã", department = "" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/classes/{classId}", staff,
            new { classCode = "", className = "Thiếu mã", department = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, "/api/v1/admin/classes/999999", staff,
            new { classCode = "X1", className = "Không có", department = "" })).StatusCode);
    }

    [Fact]
    public async Task Delete_OnlyEmptyUnusedClasses()
    {
        var staff = await StaffAsync();
        var emptyId = await CreateClassAsync(staff, $"DEL{Suffix()}");
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/classes/{emptyId}", staff, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/classes/{emptyId}", staff, null)).StatusCode);

        var usedId = await CreateClassAsync(staff, $"USE{Suffix()}");
        var studentId = await CreateStudentAsync(staff, usedId);
        var withStudents = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/classes/{usedId}", staff, null);
        Assert.Equal(HttpStatusCode.Conflict, withStudents.StatusCode);
        Assert.Equal("business_rule_violation", await ErrorCodeAsync(withStudents));

        // Once the student has left, the class can go.
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/students/{studentId}", staff, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/classes/{usedId}", staff, null)).StatusCode);
    }

    [Fact]
    public async Task RemoveMonitor_ReturnsTheStudentRole()
    {
        var staff = await StaffAsync();
        var classId = await CreateClassAsync(staff, $"MON{Suffix()}");
        var studentId = await CreateStudentAsync(staff, classId);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/classes/{classId}/monitor/{studentId}", staff, null)).StatusCode);
        Assert.Equal("Monitor", (await DataAsync(await _api.GetAsync($"/api/v1/admin/students/{studentId}", staff))).GetProperty("role").GetString());

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/classes/{classId}/monitor", staff, null)).StatusCode);
        var student = await DataAsync(await _api.GetAsync($"/api/v1/admin/students/{studentId}", staff));
        Assert.Equal("Student", student.GetProperty("role").GetString());
        Assert.False(student.GetProperty("isClassMonitor").GetBoolean());
        var row = (await DataAsync(await _api.GetAsync("/api/v1/admin/classes", staff))).EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == classId);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("monitorStudentId").ValueKind);

        var again = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/classes/{classId}/monitor", staff, null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task ClassManagement_RequiresBusinessAdmin()
    {
        var student = await _api.LoginAsync("SV210002", Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Delete, "/api/v1/admin/classes/1", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Delete, "/api/v1/admin/classes/1/monitor", student, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.SendAsync(HttpMethod.Put, "/api/v1/admin/classes/1", null,
            new { classCode = "X", className = "Y", department = "" })).StatusCode);
    }

    private Task<string> StaffAsync() => _api.LoginAsync(AccountTestClient.HssvCode, AccountTestClient.HssvPassword);

    private static string Suffix() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<int> CreateClassAsync(string staff, string classCode)
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/classes", staff,
            new { classCode, className = $"Lớp {classCode}", department = "Khoa Công nghệ thông tin" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateStudentAsync(string staff, int classId)
    {
        var code = $"SV{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/students", staff, new
        {
            studentCode = code,
            fullName = "Sinh viên quản lý lớp",
            email = $"{code.ToLowerInvariant()}@student.edu.vn",
            password = Password,
            campus = "Cơ sở 1",
            academicYear = "K23",
            classId
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errorCode").GetString();
    }
}
