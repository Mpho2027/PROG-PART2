namespace RaceDay.Api.Tests.Storage;

/// <summary>
/// A test that needs a real Blob Storage endpoint. It runs when RACEDAY_TEST_BLOB_CONNECTION holds a connection
/// string (the CI workflow starts the Azurite emulator and sets it) and is skipped otherwise.
/// </summary>
public sealed class AzuriteFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "RACEDAY_TEST_BLOB_CONNECTION";

    public static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionVariable);

    public AzuriteFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            Skip = $"Set {ConnectionVariable} to run Blob Storage tests (CI runs them against Azurite).";
    }
}
