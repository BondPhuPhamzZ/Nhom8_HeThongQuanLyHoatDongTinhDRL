namespace StudentActivityManagement.API.Services.Storage;

/// <summary>Settings of section "EvidenceStorage" in appsettings.</summary>
public sealed class EvidenceStorageOptions
{
    public const string SectionName = "EvidenceStorage";

    /// <summary>Folder for evidence files; relative paths are resolved against the content root. Must stay outside wwwroot.</summary>
    public string RootPath { get; set; } = "App_Data/evidence";

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxEvidencesPerRegistration { get; set; } = 5;

    /// <summary>Evidence may be submitted until this many days after the last session ends.</summary>
    public int SubmissionWindowDays { get; set; } = 7;
}
