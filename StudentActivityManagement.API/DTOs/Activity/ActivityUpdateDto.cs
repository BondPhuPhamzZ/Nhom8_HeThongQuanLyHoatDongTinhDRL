using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Activity
{
    public class ActivityUpdateDto
    {
        [Required(ErrorMessage = "Tên hoạt động không được để trống")]
        public string ActivityName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Yêu cầu chuẩn bị tối đa 2000 ký tự")]
        public string Requirements { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đơn vị tổ chức không được để trống")]
        [StringLength(150)]
        public string OrganizerName { get; set; } = string.Empty;

        public DateTime RegistrationOpenTime { get; set; }

        public DateTime RegistrationCloseTime { get; set; }

        [Range(1, 10000, ErrorMessage = "Số lượng tham gia phải từ 1 trở lên")]
        public int MaxParticipants { get; set; }

        [Range(0, 100, ErrorMessage = "Điểm rèn luyện phải từ 0 trở lên")]
        public int TrainingPoints { get; set; }

        public string TargetAudience { get; set; } = "Tất cả sinh viên";

        public string Status { get; set; } = "Published";

        [Required, MinLength(1, ErrorMessage = "Hoạt động phải có ít nhất một buổi.")]
        public List<ActivitySessionInputDto> Sessions { get; set; } = [];

        public List<ActivityAudienceRuleInputDto> AudienceRules { get; set; } = [];
    }

    public class UpdateActivityStatusDto
    {
        [Required(ErrorMessage = "Trạng thái mới không được để trống")]
        public string Status { get; set; } = string.Empty; // Draft, Published, Ongoing, Completed, Cancelled
    }
}
