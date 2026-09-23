using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Activity
{
    public class ActivityCreateDto
    {
        [Required(ErrorMessage = "Tên hoạt động không được để trống")]
        public string ActivityName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Địa điểm không được để trống")]
        public string Location { get; set; } = string.Empty;

        public string Campus { get; set; } = "Cơ sở 1";

        [Required(ErrorMessage = "Thời gian bắt đầu không được để trống")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Thời gian kết thúc không được để trống")]
        public DateTime EndTime { get; set; }

        [Required(ErrorMessage = "Thời gian mở đăng ký không được để trống")]
        public DateTime RegistrationOpenTime { get; set; }

        [Required(ErrorMessage = "Thời gian đóng đăng ký không được để trống")]
        public DateTime RegistrationCloseTime { get; set; }

        [Range(1, 10000, ErrorMessage = "Số lượng tham gia phải từ 1 trở lên")]
        public int MaxParticipants { get; set; } = 50;

        [Range(0, 100, ErrorMessage = "Điểm rèn luyện phải từ 0 trở lên")]
        public int TrainingPoints { get; set; } = 5;

        public string TargetAudience { get; set; } = "Tất cả sinh viên";

        public string Status { get; set; } = "Published";
    }
}
