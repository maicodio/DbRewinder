using DbRewinder.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DbRewinder;

internal class DbRewinderService : IDbRewinderAsyncService, IDbRewinderSyncService
{
    private readonly IEnumerable<IDbRewinderProvider> _providers;
    internal DbRewinderService(IServiceProvider serviceProvider)
    {
        var activators = serviceProvider.GetServices<DbRewinderActivator>();
        _providers = activators.Select(x => x.Activate(serviceProvider));
    }

    internal DbRewinderService(params IDbRewinderProvider[] providers)
    {
        this._providers = providers;
    }

    public void Install(bool reinstall=true) => 
        this.InstallAsync(reinstall).GetAwaiter().GetResult();

    /// <summary>
    /// Remove the rewind from all configured databases.
    /// </summary>
    public void Uninstall() => 
        this.UninstallAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Create a checkpoint to allow doing a partial rewind.
    /// </summary>
    public void CreateCheckpoint(string checkpointName) => 
        this.CreateCheckpointAsync(checkpointName).GetAwaiter().GetResult();

    /// <summary>
    /// Rewind all databases to a previous point.
    /// If no checkpoint or an inexistent checkpoint is providen, 
    /// than the databases will be full rewind to install point.
    /// </summary>
    public void Rewind(string? checkpointName = null) => 
        this.RewindAsync(checkpointName).GetAwaiter().GetResult();

    /// <summary>
    /// (Re)Install the rewind in all configured databases
    /// </summary>
    public Task InstallAsync(bool reinstall=true) =>
        Task.WhenAll(_providers.Select(provider => provider.InstallRewinderAsync(reinstall)));

    /// <summary>
    /// Remove the rewind from all configured databases.
    /// </summary>
    public Task UninstallAsync() => 
        Task.WhenAll(_providers.Select(provider => provider.UninstallRewinderAsync()));

    /// <summary>
    /// Create a checkpoint to allow doing a partial rewind.
    /// </summary>
    public Task CreateCheckpointAsync(string checkpointName) => 
        Task.WhenAll(_providers.Select(provider => provider.CreateRewinderCheckpoint(checkpointName)));

    /// <summary>
    /// Rewind all databases to a previous point.
    /// If no checkpoint or an inexistent checkpoint is providen, 
    /// than the databases will be full rewind to install point.
    /// </summary>
    public Task RewindAsync(string? checkpointName = null)
        => Task.WhenAll(_providers.Select(provider => provider.RewindAsync(checkpointName)));
}