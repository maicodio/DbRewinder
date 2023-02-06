using DbRewinder.Providers;
using System.Data.Common;

namespace DbRewinder;

public static class DbRewinderServiceFactory
{
    private static DbRewinderService GetService(DbRewinderProviderType rewinderProvider, Func<DbConnection> connectionFactory)
    {
        var provider = CreateProvider(rewinderProvider, connectionFactory);
        return new DbRewinderService(provider);
    }

    /// <summary>Get the Async Rewinder service</summary>
    public static IDbRewinderAsyncService GetAsyncService(DbRewinderProviderType rewinderProvider, Func<DbConnection> connectionFactory)
        => (IDbRewinderAsyncService)GetService( rewinderProvider, connectionFactory);

    /// <summary>Get the Sync Rewinder service</summary>
    public static IDbRewinderSyncService GetSyncService(DbRewinderProviderType rewinderProvider, Func<DbConnection> connectionFactory)
        => (IDbRewinderSyncService)GetService(rewinderProvider, connectionFactory);
    
    internal static IDbRewinderProvider CreateProvider(DbRewinderProviderType rewinderProvider, Func<DbConnection> connectionFactory)
    {
        switch (rewinderProvider)
        {
            case DbRewinderProviderType.MySql:
                return new MySqlRewinderProvider(connectionFactory);
            default:
                throw new KeyNotFoundException(nameof(rewinderProvider));
        }
    }
}