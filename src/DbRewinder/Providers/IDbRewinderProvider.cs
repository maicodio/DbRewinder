namespace DbRewinder.Providers;

internal interface IDbRewinderProvider
{
    Task InstallRewinderAsync(bool reinstall=true);

    Task UninstallRewinderAsync();

    Task RewindAsync(string? toCheckpointName = null);

    Task CreateRewinderCheckpoint(string checkpointName);
}