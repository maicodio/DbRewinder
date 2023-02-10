[![Test On Push](https://github.com/maicodio/DbRewinder/actions/workflows/push.yaml/badge.svg)](https://github.com/maicodio/DbRewinder/actions/workflows/push.yaml)

# DbRewinder
Db Rewinder is a time machine for reverting the integration tests data changes. It can be useful if you had integration tests that changes another integration tests data causing it to fail if running it in wrong order.

## How it works

1. When you call the install method, the service will generate a table to store reversion SQL commands for every database change.
2. Then it will generate triggers for every table. These triggers will store in the previous generated table the commands for reversion.
3. When you call rewind all reverse commands in that table will run, reverting all previous changes.

## Suported Databases and Providers

Only MySql 8 with MysqlConnector has been tested.

## Using it

### Setup

- You can setup it directly using the factory:

```csharp
    var rewindService = DbRewinderServiceFactory.GetSyncService(DbRewinderProviderType.MySql,
        () => ContainerInstance.GetService<ICoreDbContext>().Database.GetDbConnection());

    // Or the async version:

    var rewindService = DbRewinderServiceFactory.GetAsyncService(DbRewinderProviderType.MySql,
        () => ContainerInstance.GetService<ICoreDbContext>().Database.GetDbConnection());

```

- You can use it with Dependency Injection:

```csharp

    serviceCollection.ConfigureRewinder(config => config
        .AddProvider(DbRewinderProviderType.MySql, serviceProvider => serviceProvider.GetService<IMyDbContext>().Database.GetConnection())
        .AddProvider(DbRewinderProviderType.MySql, _ => new MySqlConnection(connectionString2)));

    ...

    var syncService = serviceProvider.GetService<IDbRewinderSyncService>();
    
    //or
    
    var asyncService = serviceProvider.GetService<IDbRewinderAsyncService>();

```

- Before start using the service you must run the install method once

```csharp

    rewindService.Install();

```

### Using

```csharp
    
    rewindService.Install();

    // Do some changes

    rewindService.CreateCheckpoint("My Checkpoint 1"); 

    // Do some more changes

    rewindService.Rewind("My Checkpoint 1"); 

    // Do some more changes

    rewindService.CreateCheckpoint("My Checkpoint 2"); 

    // Do some more changes

    rewindService.Rewind(); // this will rewind all data changes since install call, 

```

### Licence

MIT

