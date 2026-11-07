using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RaceDay.Api.Storage;

namespace RaceDay.Api.Tests.Storage;

/// <summary>The real Azure Blob Storage implementation, run against Azurite in CI.</summary>
public class BlobImageStorageTests
{
    private static BlobImageStorage CreateStorage(string? connectionString, string container = "raceday-tests") =>
        new(Options.Create(new BlobStorageOptions { ConnectionString = connectionString, ContainerName = container }),
            NullLogger<BlobImageStorage>.Instance);

    [AzuriteFact]
    public async Task SaveReadAndDelete_RoundTripsTheImage()
    {
        var storage = CreateStorage(AzuriteFactAttribute.ConnectionString);
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };

        var name = await storage.SaveAsync("events/1", new MemoryStream(bytes), "image/png", ".png");

        Assert.StartsWith("events/1/", name);
        Assert.EndsWith(".png", name);

        var stored = await storage.GetAsync(name);
        Assert.NotNull(stored);
        Assert.Equal("image/png", stored!.ContentType);
        using var copy = new MemoryStream();
        await stored.Content.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());

        await storage.DeleteAsync(name);
        Assert.Null(await storage.GetAsync(name));
    }

    [AzuriteFact]
    public async Task Get_MissingBlob_ReturnsNull()
    {
        var storage = CreateStorage(AzuriteFactAttribute.ConnectionString);

        Assert.Null(await storage.GetAsync($"events/missing/{Guid.NewGuid():N}.png"));
    }

    [Fact]
    public async Task NotConfigured_ThrowsImageStorageException()
    {
        var storage = CreateStorage(connectionString: "");

        await Assert.ThrowsAsync<ImageStorageException>(() =>
            storage.SaveAsync("events/1", new MemoryStream(new byte[] { 1 }), "image/png", ".png"));
    }
}
