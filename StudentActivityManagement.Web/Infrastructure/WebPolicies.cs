namespace StudentActivityManagement.Web.Infrastructure;

/// <summary>Authorization policies of the Web app, mirroring the API roles.</summary>
public static class WebPolicies
{
    public const string Student = "StudentOrMonitor";
    public const string Staff = "BusinessAdmin";

    public static bool IsStaff(System.Security.Claims.ClaimsPrincipal user) => user.IsInRole("HSSV") || user.IsInRole("SystemAdmin");

    public static string RoleLabel(string? role) => role switch
    {
        "Student" => "Sinh viên",
        "Monitor" => "Lớp trưởng",
        "HSSV" => "Phòng QL HSSV",
        "SystemAdmin" => "Quản trị hệ thống",
        _ => role ?? string.Empty
    };
}
