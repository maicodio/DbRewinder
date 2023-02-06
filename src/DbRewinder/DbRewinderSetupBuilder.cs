using System.Data.Common;
using DbRewinder.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DbRewinder;

public enum DbRewinderProviderType
{
    MySql = 1
}

public class DbRewinderSetupBuilder
{
    private readonly IServiceCollection _serviceCollection;

    public DbRewinderSetupBuilder(IServiceCollection serviceCollection)
    {
        this._serviceCollection = serviceCollection;
    }

    public DbRewinderSetupBuilder AddProvider(DbRewinderProviderType providerType, Func<IServiceProvider, DbConnection> connectionFactory)
    {
        this._serviceCollection.AddTransient<DbRewinderActivator>(serviceProvider => new DbRewinderActivator(providerType, connectionFactory));

        return this;
    }
}