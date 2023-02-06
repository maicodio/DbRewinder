namespace DbRewinder;

public interface IDbRewinderSyncService
{
    /// <summary>
    /// (Re)Install the rewind in all configured databases
    /// </summary>
    void Install(bool reinstall=true);

    /// <summary>
    /// Remove the rewind from all configured databases.
    /// </summary>
    void Uninstall();

    /// <summary>
    /// Create a checkpoint to allow doing a partial rewind.
    /// </summary>
    void CreateCheckpoint(string checkpointName);

    /// <summary>
    /// Rewind all databases to a previous point.
    /// If no checkpoint or an inexistent checkpoint is providen, 
    /// than the databases will be full rewind to install point.
    /// </summary>
    void Rewind(string? checkpointName = null);
    
}