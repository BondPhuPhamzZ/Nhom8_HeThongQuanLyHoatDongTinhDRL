using System.Text.RegularExpressions;

namespace StudentActivityManagement.API.Services.Assistant;

/// <summary>
/// Removes personal data from text before it is sent to the free Gemini tier, whose terms say not to submit personal
/// information (US-32). Known values of the signed-in user (name, student code, email, phone) are removed exactly;
/// patterns catch other student codes, emails, Vietnamese phone numbers and ID card numbers.
/// </summary>
public static partial class PersonalDataRedactor
{
    public const string Placeholder = "[đã ẩn]";

    public sealed record KnownValues(string? FullName, string? StudentCode, string? Email, string? Phone);

    public static string Redact(string text, KnownValues? known, out bool redacted)
    {
        var result = text;
        if (known != null)
        {
            foreach (var value in new[] { known.FullName, known.StudentCode, known.Email, known.Phone })
            {
                if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 3) continue;
                result = Regex.Replace(result, Regex.Escape(value.Trim()), Placeholder, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }

        result = Email().Replace(result, Placeholder);
        result = StudentCode().Replace(result, Placeholder);
        result = Phone().Replace(result, Placeholder);
        result = IdCard().Replace(result, Placeholder);
        redacted = !string.Equals(result, text, StringComparison.Ordinal);
        return result;
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    /// <summary>Codes such as SV210002, HSSV001 or "MSSV 210002": 2–4 letters followed by 3–10 digits.</summary>
    [GeneratedRegex(@"\b(?:MSSV\s*[:#]?\s*\d{3,10}|[A-Za-z]{2,4}\d{3,10})\b", RegexOptions.IgnoreCase)]
    private static partial Regex StudentCode();

    /// <summary>Vietnamese mobile numbers: 0 or +84 followed by 9 digits, optionally grouped with spaces, dots or dashes.</summary>
    [GeneratedRegex(@"(?<![\d+])(?:\+?84|0)(?:[\s.-]?\d){9}(?!\d)")]
    private static partial Regex Phone();

    /// <summary>Citizen ID (12 digits) or old ID card (9 digits).</summary>
    [GeneratedRegex(@"(?<!\d)(?:\d{12}|\d{9})(?!\d)")]
    private static partial Regex IdCard();
}
