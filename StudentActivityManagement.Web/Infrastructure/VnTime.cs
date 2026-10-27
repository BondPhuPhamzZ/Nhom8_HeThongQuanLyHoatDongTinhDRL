namespace StudentActivityManagement.Web.Infrastructure;

/// <summary>The API speaks UTC; people using the Web think in Vietnam time (UTC+7, no daylight saving).</summary>
public static class VnTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTime FromUtc(DateTime utc) =>
        DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset), DateTimeKind.Unspecified);

    /// <summary>A value typed into a datetime-local input is Vietnam wall-clock time.</summary>
    public static DateTimeOffset ToOffset(DateTime local) =>
        new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Offset);

    public static string Format(DateTime utc) => FromUtc(utc).ToString("HH:mm dd/MM/yyyy");

    public static string FormatDate(DateTime utc) => FromUtc(utc).ToString("dd/MM/yyyy");

    public static DateTime Now => FromUtc(DateTime.UtcNow);
}
