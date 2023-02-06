using System.Data.Common;
using DbRewinder.Providers;

namespace DbRewinder;

internal class DbRewinderActivator
{
    private readonly DbRewinderProviderType _rewindProvider;
    private readonly Func<IServiceProvider, DbConnection> _connectionFactory;

    public DbRewinderActivator(
        DbRewinderProviderType rewindProvider,
        Func<IServiceProvider, DbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
        _rewindProvider = rewindProvider;
    }

    public IDbRewinderProvider Activate(IServiceProvider serviceProvider)
    {
        return DbRewinderServiceFactory.CreateProvider(this._rewindProvider, () => _connectionFactory.Invoke(serviceProvider));
    }

}