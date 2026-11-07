namespace RaceDay.Api.Storage;

/// <summary>Settings for the "BlobStorage" section of appsettings.json.</summary>
public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>
    /// Azure Storage connection string (Azure portal: Storage account, Access keys).
    /// "UseDevelopmentStorage=true" points to the local Azurite emulator.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Blob container that holds the images. Created automatically if it does not exist.</summary>
    public string ContainerName { get; set; } = "raceday-images";
}
