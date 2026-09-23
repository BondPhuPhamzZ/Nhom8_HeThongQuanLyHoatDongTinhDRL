namespace StudentActivityManagement.API.DTOs.Student
{
    public class StudentResponseDto
    {
        public int Id { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Campus { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public int? ClassId { get; set; }
        public string? ClassCode { get; set; }
        public string? ClassName { get; set; }
        public bool IsClassMonitor { get; set; }
        public int AccumulatedPoints { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
