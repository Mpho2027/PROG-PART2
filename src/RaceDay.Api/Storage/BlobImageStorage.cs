using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Storage;

/// <summary>
/// Stores images in an Azure Blob Storage container. The container is private: images are never
/// served from Azure directly, they are streamed to clients through the API.
/// </summary>
public class BlobImageStorage : IImageStorage
{
    private readonly BlobStorageOptions _options;
    private readonly ILogger<BlobImageStorage> _logger;
    private readonly SemaphoreSlim _initialiseLock = new(1, 1);
    private BlobContainerClient? _container;

    public BlobImageStorage(IOptions<BlobStorageOptions> options, ILogger<BlobImageStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SaveAsync(string folder, Stream content, string contentType, string extension, CancellationToken cancellationToken = default)
    {
        var blobName = $"{folder}/{Guid.NewGuid():N}{extension}";
        var container = await GetContainerAsync(cancellationToken);
        try
        {
            await container.GetBlobClient(blobName).UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "public, max-age=3600" }
            }, cancellationToken);
            return blobName;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogError(ex, "Uploading blob {BlobName} failed", blobName);
            throw new ImageStorageException("The image could not be saved to Azure Blob Storage.", ex);
        }
    }

    public async Task<StoredImage?> GetAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        try
        {
            var download = await container.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new StoredImage(download.Value.Content, download.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogError(ex, "Downloading blob {BlobName} failed", blobName);
            throw new ImageStorageException("The image could not be read from Azure Blob Storage.", ex);
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        try
        {
            await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogError(ex, "Deleting blob {BlobName} failed", blobName);
            throw new ImageStorageException("The image could not be deleted from Azure Blob Storage.", ex);
        }
    }

    /// <summary>Connects on first use (so the API still starts when storage is down) and creates the container if needed.</summary>
    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null) return _container;
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new ImageStorageException("Azure Blob Storage is not configured. Set BlobStorage:ConnectionString.");

        await _initialiseLock.WaitAsync(cancellationToken);
        try
        {
            if (_container is null)
            {
                var clientOptions = new BlobClientOptions();
                clientOptions.Retry.MaxRetries = 2;
                clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(15);

                var container = new BlobContainerClient(_options.ConnectionString, _options.ContainerName, clientOptions);
                await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                _container = container;
            }
            return _container;
        }
        catch (Exception ex) when (IsStorageFailure(ex) || ex is FormatException or ArgumentException)
        {
            _logger.LogError(ex, "Could not connect to Azure Blob Storage container {Container}", _options.ContainerName);
            throw new ImageStorageException("Azure Blob Storage is not reachable. Check BlobStorage:ConnectionString.", ex);
        }
        finally
        {
            _initialiseLock.Release();
        }
    }

    private static bool IsStorageFailure(Exception ex) =>
        ex is RequestFailedException or AggregateException or HttpRequestException or TimeoutException;
}
