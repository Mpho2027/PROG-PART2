using System.Collections.Concurrent;
using RaceDay.Api.Storage;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>Keeps "uploaded" images in memory so the API's image endpoints can be tested without Azure.</summary>
public class FakeImageStorage : IImageStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _blobs = new();

    /// <summary>When true every call fails as if Azure Blob Storage were down.</summary>
    public bool Unavailable { get; set; }

    public IReadOnlyCollection<string> BlobNames => _blobs.Keys.ToList();

    public async Task<string> SaveAsync(string folder, Stream content, string contentType, string extension, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        var name = $"{folder}/{Guid.NewGuid():N}{extension}";
        _blobs[name] = (copy.ToArray(), contentType);
        return name;
    }

    public Task<StoredImage?> GetAsync(string blobName, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        return Task.FromResult(_blobs.TryGetValue(blobName, out var blob)
            ? new StoredImage(new MemoryStream(blob.Bytes), blob.ContentType)
            : null);
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }

    private void ThrowIfUnavailable()
    {
        if (Unavailable) throw new ImageStorageException("Azure Blob Storage is not reachable.");
    }
}
