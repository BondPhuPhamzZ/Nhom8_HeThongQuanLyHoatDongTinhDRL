using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.API.Services.Settings;

namespace StudentActivityManagement.API.DTOs.Activity;

public sealed class CheckInConfigDto
{
    public int ActivityId { get; set; }
    public string Method { get; set; } = string.Empty;
    public int OpenBeforeMinutes { get; set; }
    public int CloseAfterMinutes { get; set; }
    public int CorrectionWindowMinutes { get; set; }
    public bool IsDefault { get; set; }
    public bool HasCheckIns { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CheckInConfigUpdateDto
{
    [Required]
    public string Method { get; set; } = string.Empty;

    [Range(0, 240)]
    public int OpenBeforeMinutes { get; set; }

    [Range(0, 240)]
    public int CloseAfterMinutes { get; set; }

    [Range(SettingDefaults.MinCorrectionWindowMinutes, SettingDefaults.MaxCorrectionWindowMinutes)]
    public int? CorrectionWindowMinutes { get; set; }
}

public sealed class ReminderScheduleDto
{
    public int ActivityId { get; set; }
    public List<int> OffsetsMinutes { get; set; } = [];
    public bool IsDefault { get; set; }
}

public sealed class ReminderScheduleUpdateDto
{
    [Required]
    public List<int> OffsetsMinutes { get; set; } = [];
}
