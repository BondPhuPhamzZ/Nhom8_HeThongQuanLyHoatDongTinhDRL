using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace StudentActivityManagement.API.Services.Storage;

/// <summary>Keeps evidence files in a local folder outside wwwroot, so they are only reachable through the API.</summary>
public sealed partial class LocalEvidenceFileStorage : IEvidenceFileStorage
{
    private readonly string _rootPath;

    public LocalEvidenceFileStorage(IOptions<EvidenceStorageOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.RootPath;
        _rootPath = Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured));
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        if (!ExtensionPattern().IsMatch(extension))
        {
            throw new ArgumentException("Extension must look like '.png'.", nameof(extension));
        }

        Directory.CreateDirectory(_rootPath);
        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using var file = new FileStream(
            Path.Combine(_rootPath, storedFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
        return storedFileName;
    }

    public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveStoredPath(storedFileName);
        Stream? stream = path != null && File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveStoredPath(storedFileName);
        if (path != null && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Only names this class generated are accepted, which rules out path traversal.</summary>
    private string? ResolveStoredPath(string storedFileName) =>
        StoredNamePattern().IsMatch(storedFileName) ? Path.Combine(_rootPath, storedFileName) : null;

    [GeneratedRegex(@"^\.[A-Za-z0-9]{1,10}$")]
    private static partial Regex ExtensionPattern();

    [GeneratedRegex(@"^[0-9a-f]{32}\.[a-z0-9]{1,10}$")]
    private static partial Regex StoredNamePattern();
}
