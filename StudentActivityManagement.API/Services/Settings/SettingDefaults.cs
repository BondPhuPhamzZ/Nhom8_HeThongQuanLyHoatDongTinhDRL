using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services.Settings;

/// <summary>Defaults and limits for per-activity check-in and reminder settings (US-07, US-26).</summary>
public static class SettingDefaults
{
    public const string CheckInMethod = CheckInMethods.Code;
    public const int OpenBeforeMinutes = 15;
    public const int CloseAfterMinutes = 30;
    public const int MinWindowMinutes = 0;
    public const int MaxWindowMinutes = 240;

    /// <summary>Remind one day before each session when nothing is configured.</summary>
    public static readonly IReadOnlyList<int> ReminderOffsetsMinutes = [1440];
    public const int MinReminderOffsetMinutes = 10;
    public const int MaxReminderOffsetMinutes = 10080;
    public const int MaxReminderCount = 3;
}
