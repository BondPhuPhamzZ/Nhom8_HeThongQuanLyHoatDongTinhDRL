using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Activity;

public class ActivitySessionInputDto
{
    [Required, StringLength(120)]
    public string SessionName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Campus { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Location { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    [Range(1, 1000)]
    public int SortOrder { get; set; } = 1;
}

public class ActivitySessionResponseDto : ActivitySessionInputDto
{
    public int Id { get; set; }
}

public class ActivityAudienceRuleInputDto
{
    [StringLength(50)]
    public string? Campus { get; set; }

    [StringLength(50)]
    public string? AcademicYear { get; set; }

    public int? ClassId { get; set; }

    [Required, StringLength(20)]
    public string ParticipationType { get; set; } = "Optional";
}

public class ActivityAudienceRuleResponseDto : ActivityAudienceRuleInputDto
{
    public int Id { get; set; }
    public string? ClassCode { get; set; }
}

public class ActivityEligibilityDto
{
    public bool IsEligible { get; set; }
    public string? ParticipationType { get; set; }
    public string Reason { get; set; } = string.Empty;
}
