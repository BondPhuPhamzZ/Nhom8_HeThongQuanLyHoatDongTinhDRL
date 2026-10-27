namespace StudentActivityManagement.API.Authorization;

/// <summary>
/// Tên policy phân quyền dùng chung cho toàn bộ API.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Sinh viên và lớp trưởng.</summary>
    public const string StudentOrMonitor = "StudentOrMonitor";

    /// <summary>Nghiệp vụ Phòng Quản lý HSSV (HSSV hoặc SystemAdmin).</summary>
    public const string BusinessAdmin = "BusinessAdmin";

    /// <summary>Quản trị tài khoản và phân quyền (chỉ SystemAdmin).</summary>
    public const string SystemAdminOnly = "SystemAdminOnly";
}
