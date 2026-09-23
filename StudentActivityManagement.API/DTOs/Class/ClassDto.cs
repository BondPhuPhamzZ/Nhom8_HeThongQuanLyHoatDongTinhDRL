using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Class
{
    public class ClassDto
    {
        public int Id { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int? MonitorStudentId { get; set; }
        public string? MonitorStudentName { get; set; }
        public int TotalStudents { get; set; }
    }

    public class ClassCreateDto
    {
        [Required(ErrorMessage = "Mã lớp không được để trống")]
        public string ClassCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên lớp không được để trống")]
        public string ClassName { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;
        public int? MonitorStudentId { get; set; }
    }
}
