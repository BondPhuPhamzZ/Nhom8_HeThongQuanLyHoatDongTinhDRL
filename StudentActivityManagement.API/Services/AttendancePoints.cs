namespace StudentActivityManagement.API.Services;

/// <summary>Reasons used to distinguish legacy automatic awards and finalized attendance decisions.</summary>
public static class AttendancePoints
{
    public const string AutoReason = "Tự động ghi điểm khi điểm danh.";
    public const string FinalizedReason = "Tự động ghi điểm khi chốt điểm danh.";
    public const string AbsentReason = "Vắng: không có dữ liệu điểm danh khi chốt điểm.";
}
