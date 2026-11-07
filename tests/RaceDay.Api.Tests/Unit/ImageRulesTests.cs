using Microsoft.AspNetCore.Http;
using RaceDay.Api.Storage;

namespace RaceDay.Api.Tests.Unit;

/// <summary>Upload checks: the real file type is read from the file's first bytes.</summary>
public class ImageRulesTests
{
    private static IFormFile File(byte[] bytes, string name = "upload.png", string contentType = "image/png") =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = contentType };

    public static TheoryData<byte[], string, string> ValidImages => new()
    {
        { new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 }, "image/jpeg", ".jpg" },
        { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png", ".png" },
        { "GIF89a\u0001\u0000"u8.ToArray(), "image/gif", ".gif" },
        { "RIFF$\u0000\u0000\u0000WEBPVP8 "u8.ToArray(), "image/webp", ".webp" }
    };

    [Theory]
    [MemberData(nameof(ValidImages))]
    public void KnownImageTypes_AreAccepted(byte[] bytes, string expectedType, string expectedExtension)
    {
        Assert.True(ImageRules.TryValidate(File(bytes), out var contentType, out var extension, out _));
        Assert.Equal(expectedType, contentType);
        Assert.Equal(expectedExtension, extension);
    }

    [Fact]
    public void TextFileRenamedToPng_IsRejected()
    {
        var ok = ImageRules.TryValidate(File("not really an image"u8.ToArray()), out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("JPG, PNG, GIF or WEBP", error);
    }

    [Fact]
    public void EmptyOrMissingFile_IsRejected()
    {
        Assert.False(ImageRules.TryValidate(null, out _, out _, out _));
        Assert.False(ImageRules.TryValidate(File(Array.Empty<byte>()), out _, out _, out _));
    }

    [Fact]
    public void FileOverFiveMegabytes_IsRejected()
    {
        var big = new byte[ImageRules.MaxBytes + 1];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(big, 0);

        Assert.False(ImageRules.TryValidate(File(big), out _, out _, out var error));
        Assert.Contains("5 MB", error);
    }
}
