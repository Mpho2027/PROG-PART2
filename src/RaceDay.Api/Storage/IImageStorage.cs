namespace RaceDay.Api.Storage;

/// <summary>Stores uploaded images (event banners and profile pictures).</summary>
public interface IImageStorage
{
    /// <summary>Saves an image under the given folder and returns its blob name.</summary>
    Task<string> SaveAsync(string folder, Stream content, string contentType, string extension, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored image, or returns null when it does not exist.</summary>
    Task<StoredImage?> GetAsync(string blobName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored image. Does nothing when it does not exist.</summary>
    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);
}

/// <summary>An image read back from storage.</summary>
public sealed record StoredImage(Stream Content, string ContentType);

/// <summary>Thrown when the image store cannot be reached or is not configured.</summary>
public class ImageStorageException : Exception
{
    public ImageStorageException(string message, Exception? inner = null) : base(message, inner) { }
}
