extern alias RaceDayApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ApiProgram = RaceDayApi::Program;
using IImageStorage = RaceDayApi::RaceDay.Api.Storage.IImageStorage;
using StoredImage = RaceDayApi::RaceDay.Api.Storage.StoredImage;

namespace RaceDay.Web.Tests.Infrastructure;

/// <summary>The real RaceDay API on an in-memory database, with images kept in memory instead of Azure.</summary>
public class TestApiFactory : WebApplicationFactory<ApiProgram>
{
    private readonly string _databaseName = $"RaceDayWebTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "InMemory");
        builder.UseSetting("Database:InMemoryName", _databaseName);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IImageStorage>();
            services.AddSingleton<IImageStorage, MemoryImageStorage>();
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    private sealed class MemoryImageStorage : IImageStorage
    {
        private readonly Dictionary<string, (byte[] Bytes, string Type)> _images = new();

        public async Task<string> SaveAsync(string folder, Stream content, string contentType, string extension, CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            var name = $"{folder}/{Guid.NewGuid():N}{extension}";
            lock (_images) _images[name] = (copy.ToArray(), contentType);
            return name;
        }

        public Task<StoredImage?> GetAsync(string blobName, CancellationToken cancellationToken = default)
        {
            lock (_images)
                return Task.FromResult(_images.TryGetValue(blobName, out var image)
                    ? new StoredImage(new MemoryStream(image.Bytes), image.Type)
                    : null);
        }

        public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
        {
            lock (_images) _images.Remove(blobName);
            return Task.CompletedTask;
        }
    }
}
