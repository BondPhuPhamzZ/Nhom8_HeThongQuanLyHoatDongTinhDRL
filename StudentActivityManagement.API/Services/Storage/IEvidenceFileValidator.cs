namespace StudentActivityManagement.API.Services.Storage;

/// <summary>Checks an uploaded evidence file against the allowed types, size and file signature.</summary>
public interface IEvidenceFileValidator
{
    /// <param name="file">The uploaded file.</param>
    /// <param name="evidenceType">A value of EvidenceTypes that carries a file (Image or Document).</param>
    EvidenceFileCheck Validate(IFormFile file, string evidenceType);
}

/// <summary>
/// Result of <see cref="IEvidenceFileValidator.Validate"/>. When valid, <see cref="NormalizedExtension"/>
/// (lower case, with dot) and <see cref="ContentType"/> come from the server, not from the client.
/// </summary>
public sealed record EvidenceFileCheck(bool IsValid, string? ErrorMessage, string? NormalizedExtension, string? ContentType)
{
    public static EvidenceFileCheck Invalid(string message) => new(false, message, null, null);

    public static EvidenceFileCheck Valid(string extension, string contentType) => new(true, null, extension, contentType);
}
