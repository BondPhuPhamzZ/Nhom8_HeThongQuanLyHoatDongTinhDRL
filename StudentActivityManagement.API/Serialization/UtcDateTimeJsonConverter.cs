using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudentActivityManagement.API.Serialization;

/// <summary>
/// One time contract for every client: input must carry an offset ("Z" or "+07:00") and is stored as UTC;
/// output is always UTC with a "Z" suffix. A time without offset is rejected (400) instead of being guessed,
/// because a Vietnamese client sending local time would otherwise shift registration windows by 7 hours.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ||
            !HasOffset(text))
        {
            throw new JsonException("Thời gian phải theo ISO 8601 và có múi giờ, ví dụ 2026-11-02T08:00:00+07:00 hoặc 2026-11-02T01:00:00Z.");
        }

        return value.UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToUtc(value).ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));

    /// <summary>Values read from SQLite come back Unspecified; they were written as UTC.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static bool HasOffset(string text)
    {
        var timeStart = text.IndexOf('T');
        if (timeStart < 0) return false;
        var time = text[(timeStart + 1)..];
        return time.EndsWith('Z') || time.EndsWith('z') || time.Contains('+') || time.Contains('-');
    }
}
