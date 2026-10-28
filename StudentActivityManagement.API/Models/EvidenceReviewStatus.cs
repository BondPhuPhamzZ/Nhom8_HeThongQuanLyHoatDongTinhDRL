namespace StudentActivityManagement.API.Models;

/// <summary>Review state of a piece of evidence. Set to Accepted/Rejected by Phòng QL HSSV (US-21).</summary>
public static class EvidenceReviewStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Pending, Accepted, Rejected];
}
