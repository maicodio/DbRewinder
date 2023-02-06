namespace DbRewinder;

public interface IDbRewinderAsyncService
{
    /// <summary>
    /// (Re)Install the rewind in all configured databases
    /// </summary>
    Task InstallAsync(bool reinstall=true);

    /// <summary>
    /// Remove the rewind from all configured databases.
    /// </summary>
    Task UninstallAsync();

    /// <summary>
    /// Create a checkpoint to allow doing a partial rewind.
    /// </summary>
    Task CreateCheckpointAsync(string checkpointName);

    /// <summary>
    /// Rewind all databases to a previous point.
    /// If no checkpoint or an inexistent checkpoint is providen, 
    /// than the databases will be full rewind to install point.
    /// </summary>
    Task RewindAsync(string? checkpointName = null);
}