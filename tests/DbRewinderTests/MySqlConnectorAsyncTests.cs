using Microsoft.Extensions.DependencyInjection;

namespace DbRewinderTests;

[Collection("MySqlConnectorAsyncTests")]
public class MySqlConnectorAsyncTests
{
    const string CONNECTION_STRING = "Data Source=localhost;Persist Security Info=True;User ID=root;Password=123456;AllowUserVariables=True";
    const string CONNECTION_STRING_DATABASE = "Data Source=localhost;Initial Catalog=DbRewinderTest;Persist Security Info=True;User ID=root;Password=123456;AllowUserVariables=True";
    const string DATABASE_NAME = "DbRewinderTest";
    
    private readonly IDbRewinderAsyncService dbRewinderAsyncService;

    private readonly Random rnd = new Random();

    public MySqlConnectorAsyncTests()
    {
        dbRewinderAsyncService = DbRewinderServiceFactory.GetAsyncService(DbRewinderProviderType.MySql, () => new MySqlConnection(CONNECTION_STRING_DATABASE));
    }

    private async Task CreateDatabaseAsync()
    {
        using var connection = new MySqlConnection(CONNECTION_STRING);

        await connection.OpenAsync();

        var command = connection.CreateCommand();

        command.CommandText = $"DROP DATABASE  IF EXISTS `{DATABASE_NAME}`";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"CREATE DATABASE `{DATABASE_NAME}`";
        await command.ExecuteNonQueryAsync();

        await connection.ChangeDatabaseAsync(DATABASE_NAME);

        command.CommandText = @"
CREATE TABLE Person (
    Id BIGINT auto_increment NOT NULL,
    Name varchar(100) NOT NULL,
    Age INT NOT NULL,
    `Status` varchar(50) GENERATED ALWAYS AS ((CASE WHEN Age < 0 THEN 'EGG' WHEN Age BETWEEN 0 AND 2 THEN 'BABY' WHEN Age BETWEEN 3 AND 11 THEN 'KID' WHEN Age BETWEEN 12 AND 19 THEN 'TEENAGER' WHEN Age BETWEEN 20 AND 59 THEN 'ADULT' WHEN Age BETWEEN 60 AND 140 THEN 'ELDERY' ELSE 'NOT HUMAN' END )) STORED,
    Obs varchar(255) CHARACTER SET latin1 COLLATE latin1_swedish_ci DEFAULT NULL,
	CONSTRAINT Person_PK PRIMARY KEY (Id)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;
";

        await command.ExecuteNonQueryAsync();

        command.CommandText = @"
CREATE TABLE PersonTag (
	PersonId BIGINT NOT NULL,
	Tag varchar(100) NOT NULL,
	CONSTRAINT PersonTag_PK PRIMARY KEY (PersonId,Tag),
	CONSTRAINT PersonTag_FK FOREIGN KEY (PersonId) REFERENCES Person(Id) ON DELETE CASCADE ON UPDATE CASCADE
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;
";

        await command.ExecuteNonQueryAsync();

        command.CommandText = "CREATE INDEX PersonTag_PersonId_IDX USING BTREE ON PersonTag (PersonId);";

        await command.ExecuteNonQueryAsync();

        command.CommandText = @"
CREATE TABLE House (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    Address VARCHAR(100) NOT NULL,
    OwnerPersonId BIGINT NULL,
    Obs VARCHAR(1000) NULL,
    ExternalId BIGINT NULL,
    Value decimal(20,10) NULL,
    CONSTRAINT House_PK PRIMARY KEY (Id),
    CONSTRAINT House_FK FOREIGN KEY (OwnerPersonId) REFERENCES Person(Id) ON DELETE RESTRICT ON UPDATE RESTRICT
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;";

        await command.ExecuteNonQueryAsync();

        command.CommandText = "CREATE INDEX House_OwnerPersonId_IDX USING BTREE ON House (OwnerPersonId);";

        await command.ExecuteNonQueryAsync();
    }

    private async ValueTask<long> CheckDatabaseAsync()
    {
        using var connection = new MySqlConnection(CONNECTION_STRING_DATABASE);

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        
        command.CommandText = "SELECT COUNT(1) FROM __REWINDER_DATA;";

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task GenerateDataAsync()
    {
        using var connection = new MySqlConnection(CONNECTION_STRING_DATABASE);

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        
        command.CommandText = "INSERT INTO Person (Name, Age) VALUES ('Person1', -1)";
        await command.ExecuteNonQueryAsync();
        var lastId = command.LastInsertedId;

        command.CommandText = $"INSERT INTO PersonTag (PersonId, Tag) VALUES ({lastId}, 'Not born')";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"INSERT INTO House (Address, OwnerPersonId, Value) VALUES ('Fourth Avenue, 10', {lastId}, 1000000.00001)";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"INSERT INTO PersonTag (PersonId, Tag) VALUES ({lastId}, 'Rich')";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"INSERT INTO PersonTag (PersonId, Tag) VALUES ({lastId}, 'Red')";
        await command.ExecuteNonQueryAsync();

        command.CommandText = "INSERT INTO Person (Name, Age) VALUES ('Person2', 89)";
        await command.ExecuteNonQueryAsync();
        lastId = command.LastInsertedId;

        command.CommandText = $"INSERT INTO PersonTag (PersonId, Tag) VALUES ({lastId}, 'Healthy')";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"INSERT INTO House (Address, OwnerPersonId, Value) VALUES ('Third Avenue, 1', {lastId}, 100000.0301)";
        await command.ExecuteNonQueryAsync();

        command.CommandText = "INSERT INTO Person (Name, Age) VALUES ('Person3', 44)";
        await command.ExecuteNonQueryAsync();

        command.CommandText = "INSERT INTO House (Address, OwnerPersonId, Value) VALUES ('First Avenue, 22', NULL, 500000.0001)";
        await command.ExecuteNonQueryAsync();

    }

    private async Task UpdateDataAsync()
    {
        var ids = new List<long>();

        using var connection = new MySqlConnection(CONNECTION_STRING_DATABASE);

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        
        command.CommandText = "SELECT Id FROM Person";
        using (var reader = await command.ExecuteReaderAsync())
        {
            while(await reader.ReadAsync())
            {
                ids.Add(reader.GetInt64(0));
            }
        }

        ids.Sort((a,b) => rnd.NextInt64().CompareTo(rnd.NextInt64()));
        
        command.CommandText = $"DELETE FROM House WHERE OwnerPersonId = {ids.First()}";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"DELETE FROM Person WHERE Id = {ids.First()}";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"INSERT INTO PersonTag (PersonId, Tag) VALUES ({ids[1]}, '{rnd.NextInt64()}')";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"UPDATE PersonTag SET Tag = CONCAT(Tag, '0') WHERE PersonId = {ids[2]};";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"DELETE FROM PersonTag WHERE PersonId = {ids[3]};";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"UPDATE House SET Address = 'Banana Street, 22' WHERE OwnerPersonId IS NULL;";
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task NewInstallSuccessTest()
    {
        // Arrange
        await CreateDatabaseAsync();

        // act
        var act1 = () => dbRewinderAsyncService.InstallAsync();
        var act2 = () => CheckDatabaseAsync().AsTask();
        
        // assert
        await act1.Should().NotThrowAsync();
        await act2.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReinstallSuccessTest()
    {
        // Arrange
        await CreateDatabaseAsync();

        // act
        var act1 = () => dbRewinderAsyncService.InstallAsync();
        var act2 = () => dbRewinderAsyncService.InstallAsync();
        var act3 = () => CheckDatabaseAsync().AsTask();
        
        // assert
        await act1.Should().NotThrowAsync();
        await act2.Should().NotThrowAsync();
        await act3.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SimpleRewindCheckpointSuccessTest()
    {
        // Arrange
        await CreateDatabaseAsync();
        await dbRewinderAsyncService.InstallAsync();

        // act
        await GenerateDataAsync();
        await dbRewinderAsyncService.CreateCheckpointAsync("TestA");
        await GenerateDataAsync();
        await UpdateDataAsync();
        await dbRewinderAsyncService.RewindAsync("TestA");
        var ret = await CheckDatabaseAsync();
        
        // assert
        ret.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SimpleRewindAllSuccessTest()
    {
        // Arrange
        await CreateDatabaseAsync();
        await dbRewinderAsyncService.InstallAsync();

        // act
        await GenerateDataAsync();
        await dbRewinderAsyncService.CreateCheckpointAsync("TestA");
        await GenerateDataAsync();
        await UpdateDataAsync();
        await dbRewinderAsyncService.RewindAsync("TestA");
        await GenerateDataAsync();        
        await UpdateDataAsync();
        await dbRewinderAsyncService.RewindAsync();
        
        var ret = await CheckDatabaseAsync();
        
        // assert
        ret.Should().Be(0);
    }

    [Fact]
    public void DependencyInjectionSuccessTest()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // act
        serviceCollection
            .ConfigureRewinder(setup => setup
                .AddProvider(DbRewinderProviderType.MySql, sp => new MySqlConnection(CONNECTION_STRING_DATABASE)));
         
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var asyncService = serviceProvider.GetService<IDbRewinderAsyncService>();
        var syncService = serviceProvider.GetService<IDbRewinderSyncService>();

        // assert
        asyncService.Should().NotBeNull();
        syncService.Should().NotBeNull();
    }
}