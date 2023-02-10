using Microsoft.Extensions.DependencyInjection;

namespace DbRewinder;

public static class DbRewinderSetupExtensions
{
    public static IServiceCollection ConfigureRewinder(this IServiceCollection serviceCollection, 
        Action<DbRewinderSetupBuilder> setup)
    {
        setup.Invoke(new DbRewinderSetupBuilder(serviceCollection));
        serviceCollection.AddTransient<IDbRewinderAsyncService>(serviceProvider => new DbRewinderService(serviceProvider));
        serviceCollection.AddTransient<IDbRewinderSyncService>(serviceProvider => new DbRewinderService(serviceProvider));
        return serviceCollection;
    }
}