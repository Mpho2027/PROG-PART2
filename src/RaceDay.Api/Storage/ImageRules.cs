namespace RaceDay.Api.Storage;

/// <summary>
/// Checks an uploaded image. The file type is worked out from the file's first bytes rather than trusting
/// the name or content type sent by the client, so a renamed non-image file is rejected.
/// </summary>
public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly (string ContentType, string Extension, byte[] Signature, int Offset)[] KnownTypes =
    {
        ("image/jpeg", ".jpg", new byte[] { 0xFF, 0xD8, 0xFF }, 0),
        ("image/png", ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0),
        ("image/gif", ".gif", "GIF8"u8.ToArray(), 0),
        ("image/webp", ".webp", "WEBP"u8.ToArray(), 8)
    };

    /// <summary>Validates the file and returns its real content type and extension, or an error message.</summary>
    public static bool TryValidate(IFormFile? file, out string contentType, out string extension, out string error)
    {
        contentType = extension = error = string.Empty;

        if (file is null || file.Length == 0)
        {
            error = "Please choose an image file to upload.";
            return false;
        }
        if (file.Length > MaxBytes)
        {
            error = "The image is too large. The maximum size is 5 MB.";
            return false;
        }

        var header = new byte[16];
        using (var stream = file.OpenReadStream())
        {
            var read = stream.Read(header, 0, header.Length);
            Array.Resize(ref header, read);
        }

        foreach (var type in KnownTypes)
        {
            if (header.Length >= type.Offset + type.Signature.Length
                && header.AsSpan(type.Offset, type.Signature.Length).SequenceEqual(type.Signature)
                && (type.Extension != ".webp" || header.AsSpan(0, 4).SequenceEqual("RIFF"u8)))
            {
                contentType = type.ContentType;
                extension = type.Extension;
                return true;
            }
        }

        error = "Only JPG, PNG, GIF or WEBP images can be uploaded.";
        return false;
    }
}
