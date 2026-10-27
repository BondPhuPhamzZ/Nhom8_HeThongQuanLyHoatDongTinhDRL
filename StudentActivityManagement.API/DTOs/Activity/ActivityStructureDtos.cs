using System.ComponentModel.DataAnnotations;

namespace StudentActivityManagement.API.DTOs.Activity;

public class ActivitySessionInputDto
{
    /// <summary>
    /// Id of an existing session when updating an activity. Sessions sent with an Id keep that Id
    /// (check-in data will reference it); sessions without an Id are created; existing sessions
    /// missing from the request are removed.
    /// </summary>
    public int? Id { get; set; }

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

public class ActivitySessionResponseDto
{
    public int Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int SortOrder { get; set; }
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
