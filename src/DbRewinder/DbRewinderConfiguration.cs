using System.Data.Common;
using DbRewinder.Providers;

namespace DbRewinder;

internal class DbRewinderSetup
{
    public IDbRewinderProvider Provider { get; private set; }

    public Func<IServiceProvider, DbConnection> ConnectionFactory {get; private set; }

    public DbRewinderSetup(IDbRewinderProvider provider, Func<IServiceProvider, DbConnection> connectionFactory)
    {
        this.Provider = provider;
        this.ConnectionFactory = connectionFactory;
    }
}