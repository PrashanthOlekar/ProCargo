using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Services;

namespace ProCargo.Infrastructure.Storage;

/// <summary>Section "FileStorage".</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Local (development) or AzureBlob.</summary>
    public string Provider { get; set; } = "Local";

    public string LocalPath { get; set; } = "App_Data/files";

    /// <summary>Either a connection string (local Azurite) or a service URI used with managed identity.</summary>
    public string? BlobConnectionString { get; set; }
    public string? BlobServiceUri { get; set; }
    public string ContainerName { get; set; } = "procargo-files";
}

/// <summary>
/// Development storage on the local disk. Keys are server-generated, but the resolved path is still checked to
/// stay inside the root so a crafted key can never escape it.
/// </summary>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.LocalPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string storageKey, string contentType, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (!File.Exists(path)) throw new NotFoundException("FILE_NOT_FOUND", "The file was not found.");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }

        return path;
    }
}

/// <summary>
/// Azure Blob Storage. In Azure the API authenticates with its managed identity (no keys in configuration);
/// the container is private and files are streamed through the API after an authorization check.
/// </summary>
internal sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public AzureBlobFileStorage(IOptions<FileStorageOptions> options)
    {
        var o = options.Value;
        var service = !string.IsNullOrWhiteSpace(o.BlobConnectionString)
            ? new BlobServiceClient(o.BlobConnectionString)
            : new BlobServiceClient(new Uri(o.BlobServiceUri ?? throw new InvalidOperationException(
                "FileStorage:BlobServiceUri or FileStorage:BlobConnectionString is required.")), new DefaultAzureCredential());
        _container = service.GetBlobContainerClient(o.ContainerName);
    }

    public async Task<string> SaveAsync(Stream content, string storageKey, string contentType, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);
        await _container.GetBlobClient(storageKey).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);
        return storageKey;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var blob = _container.GetBlobClient(storageKey);
        if (!await blob.ExistsAsync(cancellationToken)) throw new NotFoundException("FILE_NOT_FOUND", "The file was not found.");
        return await blob.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
        _container.GetBlobClient(storageKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (!_initialized)
            {
                await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }
}
