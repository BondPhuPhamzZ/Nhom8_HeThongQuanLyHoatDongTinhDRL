namespace StudentActivityManagement.API.Models
{
    public static class Role
    {
        public const string Student = "Student";
        public const string Monitor = "Monitor";
        public const string HSSV = "HSSV";
        public const string SystemAdmin = "SystemAdmin";

        public static readonly string[] StudentRoles = [Student, Monitor];
        public static readonly string[] StaffRoles = [HSSV, SystemAdmin];
        public static readonly string[] All = [Student, Monitor, HSSV, SystemAdmin];

        /// <summary>
        /// Chuẩn hóa tên role (không phân biệt hoa thường). Trả về null nếu không phải role hợp lệ.
        /// </summary>
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return All.FirstOrDefault(role => role.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        }
    }
}
