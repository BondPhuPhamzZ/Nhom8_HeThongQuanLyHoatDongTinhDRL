namespace StudentActivityManagement.API.Models;

/// <summary>
/// How attendance is taken for an activity. Code: the student enters the session code announced by the organiser.
/// Manual: Phòng QL HSSV records attendance on the participants screen (US-21).
/// </summary>
public static class CheckInMethods
{
    public const string Code = "Code";
    public const string Manual = "Manual";

    public static readonly string[] All = [Code, Manual];

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return All.FirstOrDefault(method => method.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
