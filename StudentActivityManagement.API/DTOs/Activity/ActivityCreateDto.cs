using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Activity
{
    public class ActivityCreateDto
    {
        [Required(ErrorMessage = "Tên hoạt động không được để trống")]
        public string ActivityName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đơn vị tổ chức không được để trống")]
        [StringLength(150)]
        public string OrganizerName { get; set; } = string.Empty;

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

        [Required, MinLength(1, ErrorMessage = "Hoạt động phải có ít nhất một buổi.")]
        public List<ActivitySessionInputDto> Sessions { get; set; } = [];

        public List<ActivityAudienceRuleInputDto> AudienceRules { get; set; } = [];
    }
}
