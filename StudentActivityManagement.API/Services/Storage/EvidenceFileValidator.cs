using Microsoft.Extensions.Options;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services.Storage;

public sealed class EvidenceFileValidator : IEvidenceFileValidator
{
    private sealed record FileKind(string EvidenceType, string ContentType, byte[] Signature);

    // The extension decides the expected signature; the client's Content-Type header is ignored.
    private static readonly Dictionary<string, FileKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new(EvidenceTypes.Image, "image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".jpeg"] = new(EvidenceTypes.Image, "image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".png"] = new(EvidenceTypes.Image, "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        [".pdf"] = new(EvidenceTypes.Document, "application/pdf", "%PDF-"u8.ToArray())
    };

    private readonly EvidenceStorageOptions _options;

    public EvidenceFileValidator(IOptions<EvidenceStorageOptions> options)
    {
        _options = options.Value;
    }

    public EvidenceFileCheck Validate(IFormFile file, string evidenceType)
    {
        if (file.Length == 0)
        {
            return EvidenceFileCheck.Invalid("File minh chứng đang trống.");
        }
        if (file.Length > _options.MaxFileSizeBytes)
        {
            return EvidenceFileCheck.Invalid($"File minh chứng tối đa {_options.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!Kinds.TryGetValue(extension, out var kind) || kind.EvidenceType != evidenceType)
        {
            return EvidenceFileCheck.Invalid(evidenceType == EvidenceTypes.Document
                ? "Minh chứng tài liệu chỉ nhận file .pdf."
                : "Minh chứng hình ảnh chỉ nhận file .jpg, .jpeg hoặc .png.");
        }

        var header = new byte[kind.Signature.Length];
        using (var stream = file.OpenReadStream())
        {
            var read = 0;
            while (read < header.Length)
            {
                var count = stream.Read(header, read, header.Length - read);
                if (count == 0) break;
                read += count;
            }
            if (read < header.Length || !header.AsSpan().SequenceEqual(kind.Signature))
            {
                return EvidenceFileCheck.Invalid("Nội dung file không khớp với định dạng của phần mở rộng.");
            }
        }

        return EvidenceFileCheck.Valid(extension.ToLowerInvariant(), kind.ContentType);
    }
}
