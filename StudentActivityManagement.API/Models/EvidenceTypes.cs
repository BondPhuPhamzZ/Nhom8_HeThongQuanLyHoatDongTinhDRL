namespace StudentActivityManagement.API.Models;

/// <summary>Kinds of participation evidence: an image or PDF file, or a confirmation code typed in by the student.</summary>
public static class EvidenceTypes
{
    public const string Image = "Image";
    public const string Document = "Document";
    public const string ConfirmationCode = "ConfirmationCode";

    public static readonly string[] All = [Image, Document, ConfirmationCode];

    /// <summary>Types that carry an uploaded file.</summary>
    public static readonly string[] WithFile = [Image, Document];

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return All.FirstOrDefault(type => type.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
