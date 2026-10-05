using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ProCargo.Application.Common;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Services.Partners;

/// <summary>
/// Secure file upload handling:
///   * size limit, extension allow-list and MIME allow-list per extension,
///   * magic-number check so a renamed executable cannot pass as a PDF/JPEG,
///   * server-generated storage key (never the client file name) - prevents path traversal and overwrites,
///   * SHA-256 recorded for integrity / duplicate detection,
///   * only metadata goes to SQL Server; content goes to object storage.
/// </summary>
public sealed class FileService : IFileService
{
    private readonly IFileStorage _storage;
    private readonly IDocumentRepository _documents;
    private readonly ICurrentUser _currentUser;
    private readonly FileUploadOptions _options;
    private readonly TimeProvider _clock;

    public FileService(IFileStorage storage, IDocumentRepository documents, ICurrentUser currentUser,
        IOptions<FileUploadOptions> options, TimeProvider clock)
    {
        _storage = storage;
        _documents = documents;
        _currentUser = currentUser;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<long> StoreAsync(FileUpload file, string category, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        Validate(file, extension);

        await using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0 || buffer.Length > _options.MaxFileSizeBytes)
        {
            throw Invalid($"Files must be between 1 byte and {_options.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        buffer.Position = 0;
        if (!HasValidSignature(buffer, extension))
        {
            throw Invalid("The file content does not match its type.");
        }

        buffer.Position = 0;
        var hash = await SHA256.HashDataAsync(buffer, cancellationToken);

        var now = _clock.GetUtcNow().UtcDateTime;
        var storageKey = $"{category}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";

        buffer.Position = 0;
        await _storage.SaveAsync(buffer, storageKey, file.ContentType, cancellationToken);

        var safeName = SanitizeFileName(file.FileName);
        return await _documents.CreateStoredFileAsync(
            new StoredFileCommand(storageKey, safeName, file.ContentType, buffer.Length, hash, _currentUser.UserId), cancellationToken);
    }

    public async Task<FileDownload> OpenAsync(long storedFileId, CancellationToken cancellationToken)
    {
        var file = await _documents.GetStoredFileAsync(storedFileId, cancellationToken) ?? throw NotFoundException.For("File", storedFileId);
        var stream = await _storage.OpenReadAsync(file.StorageKey, cancellationToken);
        return new FileDownload(stream, file.ContentType, file.OriginalFileName);
    }

    private void Validate(FileUpload file, string extension)
    {
        if (file.Length <= 0 || file.Length > _options.MaxFileSizeBytes)
        {
            throw Invalid($"Files must be between 1 byte and {_options.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        if (string.IsNullOrEmpty(extension) || !_options.AllowedTypes.TryGetValue(extension, out var mimeTypes))
        {
            throw Invalid($"Allowed file types: {string.Join(", ", _options.AllowedTypes.Keys)}.");
        }

        if (!mimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw Invalid("The file's content type does not match its extension.");
        }
    }

    internal static bool HasValidSignature(Stream stream, string extension)
    {
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        if (read < 4) return false;

        return extension switch
        {
            ".pdf" => header[..4].SequenceEqual("%PDF"u8),
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header[..4].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            ".webp" => read >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    internal static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c) && c != '<' && c != '>' && !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "file";
        return cleaned.Length > 200 ? cleaned[^200..] : cleaned;
    }

    private static BusinessRuleException Invalid(string message) => new(ErrorCodes.FileInvalid, message);
}
