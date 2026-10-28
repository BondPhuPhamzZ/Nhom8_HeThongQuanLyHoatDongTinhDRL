namespace StudentActivityManagement.API.Services.Storage;

/// <summary>
/// Stores evidence files under server-generated names. Callers keep the returned name in
/// ActivityEvidence.StoredFileName and must never build paths from client file names.
/// </summary>
public interface IEvidenceFileStorage
{
    /// <summary>Saves <paramref name="content"/> and returns the stored file name (GUID + <paramref name="extension"/>).</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file for reading, or returns null when it does not exist or the name is not a stored name.</summary>
    Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored file; a missing file is not an error.</summary>
    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);
}
