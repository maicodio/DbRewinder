using System.Data;
using System.Data.Common;

namespace DbRewinder.Providers;

internal class MySqlRewinderProvider: IDbRewinderProvider
{
    const string REWIND_TABLE_NAME = "__REWINDER_DATA";
    const string REWIND_TRIGGER_NAME_PREFIX = "__TR_REWINDER";
    private readonly Func<DbConnection> _connectionFactory;

    public MySqlRewinderProvider(Func<DbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DbConnection> OpenConnectionAsync()
    {
        var connection = _connectionFactory.Invoke() ?? throw new NotSupportedException("Can't create a connection");
        await connection.OpenAsync().ConfigureAwait(false);
        return connection;
    }

    public async Task InstallRewinderAsync(bool reinstall=true)
    {
        using var connection = await OpenConnectionAsync().ConfigureAwait(false);

        if (reinstall) 
        {
            await UninstallRewinderAsync().ConfigureAwait(false);;
        }
        #if NET60
        else if (await CheckConfigAsync(connection).ConfigureAwait(false))
        #else
        else if (CheckConfig(connection))
        #endif
        {
            return;
        }

        await GenerateRewinderTable(connection).ConfigureAwait(false);
        await GenerateRewinderTriggers(connection).ConfigureAwait(false);
    }
    
    public async Task UninstallRewinderAsync()
    {
        using var connection = await OpenConnectionAsync().ConfigureAwait(false);

        string databaseName = connection.Database;

        var command = connection.CreateCommand();

        command.CommandText = $"DROP TABLE IF EXISTS {REWIND_TABLE_NAME}";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        var allTriggers = connection.GetSchema("Triggers");
        var triggers = allTriggers
            .Select($"TRIGGER_SCHEMA = '{databaseName}' AND TRIGGER_NAME LIKE '{REWIND_TRIGGER_NAME_PREFIX}_%'")
            .SecureCopyToDataTable();

        foreach (DataRow trigger in triggers.Rows)
        {
            var triggerName = trigger.Field<string>("TRIGGER_NAME");
            command.CommandText = $"DROP TRIGGER IF EXISTS {triggerName};";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
    
    public async Task CreateRewinderCheckpoint(string checkpointName)
    {
        using var connection = await OpenConnectionAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        var par = command.CreateParameter();
        par.ParameterName = "sql";
        par.Value = $"-- {checkpointName.WithMaxLength(100)}";
        par.DbType = DbType.String;
        command.CommandText = $"INSERT INTO {REWIND_TABLE_NAME} (Command) VALUES (@sql);";
        command.Parameters.Add(par);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task RewindAsync(string? toCheckpointName = null)
    {
        using var connection = await OpenConnectionAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        var commandList = new List<string>();
        command.CommandText = $"SELECT Id, Command FROM {REWIND_TABLE_NAME} ORDER BY Id Desc;";
        var id = 0L;
        using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (reader.Read())
            {
                id = reader.GetInt64(0);
                var rewindCommand = reader.GetString(1);

                if (rewindCommand == $"-- {toCheckpointName}") break;
                if (rewindCommand.StartsWith("--"))
                {
                    continue;
                }

                commandList.Add(rewindCommand);
            }
        }

        if (!commandList.Any()) return;

        foreach (var rewindCommand in commandList)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Rewinding {rewindCommand}...");

                command.CommandText = rewindCommand;
                var ret = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                if (ret == 0)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: No rows affected;");
                } 
                else
                {
                    System.Diagnostics.Debug.WriteLine("OK");
                }
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        command.CommandText = $"DELETE FROM {REWIND_TABLE_NAME} WHERE Id >= {id}";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    #if NET60
    private static async ValueTask<bool> CheckConfigAsync(DbConnection connection)
    {   
        var tables = await connection.GetSchemaAsync("Tables").ConfigureAwait(false);
        return tables.Select($"TABLE_SCHEMA = '{connection.Database}' AND TABLE_NAME = '{REWIND_TABLE_NAME}'").Any();
    }
    #else
    private static bool CheckConfig(DbConnection connection)
    {   
        var tables = connection.GetSchema("Tables");
        return tables.Select($"TABLE_SCHEMA = '{connection.Database}' AND TABLE_NAME = '{REWIND_TABLE_NAME}'").Any();
    }
    #endif

    private static async Task GenerateRewinderTriggers(DbConnection connection)
    {
        var command = connection.CreateCommand();
        var allTables = connection.GetSchema("Tables");
        var tables = allTables.Select($"TABLE_SCHEMA = '{connection.Database}' AND TABLE_TYPE LIKE '%TABLE'").SecureCopyToDataTable();
        var allColumns = connection.GetSchema("Columns");
        var columns = allColumns.Select($"TABLE_SCHEMA = '{connection.Database}'").SecureCopyToDataTable();

        var i = await GenerateCascadeDeleteTriggers(connection, columns).ConfigureAwait(true);

        foreach (DataRow table in tables.Rows)
        {
            i++;
            var tableName = table.Field<string>("TABLE_NAME");

            if (tableName == null || tableName == REWIND_TABLE_NAME) continue;

            var tableColums = columns.Select($"TABLE_NAME = '{tableName}'").SecureCopyToDataTable();

            await GenerateDeleteTrigger(command, i, tableName, tableColums).ConfigureAwait(true);
            await GenerateInsertTrigger(command, i, tableName, tableColums).ConfigureAwait(true);
            await GenerateUpdateTrigger(command, i, tableName, tableColums).ConfigureAwait(true);
        }
    }

    private static async Task GenerateDeleteTrigger(DbCommand cmd, int i, string tableName, DataTable columns)
    {
        var template = $@"
CREATE TRIGGER {REWIND_TRIGGER_NAME_PREFIX}_{i:D5}_AD_{tableName.WithMaxLength(32)} AFTER DELETE ON `{tableName}`
FOR EACH ROW
BEGIN
    INSERT INTO {REWIND_TABLE_NAME} (`Command`) VALUES (CONCAT('INSERT INTO `{tableName}` ($$COLUMN_NAMES$$) VALUES (', $$COLUMN_VALUES$$, ');'));
END;";

        var colNames = string.Join(", ", columns
            .Select()
            .Select(x => GetColumnRepresentation(x))
            .Where(x => !string.IsNullOrWhiteSpace(x.columnName) && !x.generated)
            .Select(x => $"`{x.columnName}`"));

        var colValues = string.Join(", ", columns
            .Select()
            .Select(x => GetColumnRepresentation(x))
            .Where(x => !string.IsNullOrWhiteSpace(x.columnName) && !x.generated)
            .Select(x => $"{x.template.Replace("$$alias$$", "old")}, ','"));

        colValues = colValues.Remove(colValues.Length - 5);

        cmd.CommandText = template.Replace("$$COLUMN_NAMES$$", colNames).Replace("$$COLUMN_VALUES$$", colValues);
        
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task GenerateUpdateTrigger(DbCommand cmd, int i, string tableName, DataTable columns)
    {
        var template = $@"
CREATE TRIGGER {REWIND_TRIGGER_NAME_PREFIX}_{i:D5}_AU_{tableName.WithMaxLength(32)} AFTER UPDATE ON `{tableName}`
FOR EACH ROW
BEGIN
    INSERT INTO {REWIND_TABLE_NAME} (`Command`) VALUES (CONCAT('UPDATE `{tableName}` SET', $$COLUMN_SET$$, ' WHERE ', $$COLUMN_WHERE$$, ';'));
END;";

        var colSet = string.Join(", ", columns
            .Select()
            .Select(x => GetColumnRepresentation(x))
            .Where(x => !string.IsNullOrWhiteSpace(x.columnName) && !x.generated)
            .Select(x => $" '`{x.columnName}` = ', {x.template.Replace("$$alias$$", "old")}, ','"));

        colSet = colSet.Remove(colSet.Length - 5);

        var colWhere = string.Join(", ' AND ', ", columns
            .Select()
            .Select(x => GetColumnRepresentation(x))
            .Where(x => !string.IsNullOrWhiteSpace(x.columnName) && !x.generated)
            .Select(x => $"'`{x.columnName}`', CASE WHEN new.`{x.columnName}` IS NULL THEN 'IS NULL' ELSE CONCAT(' = ', {x.template.Replace("$$alias$$", "new")}) END"));

        cmd.CommandText = template.Replace("$$COLUMN_SET$$", colSet).Replace("$$COLUMN_WHERE$$", colWhere);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task GenerateInsertTrigger(DbCommand cmd, int i, string tableName, DataTable columns)
    {
        var template = $@"
CREATE TRIGGER {REWIND_TRIGGER_NAME_PREFIX}_{i:D5}_AI_{tableName.WithMaxLength(32)} AFTER INSERT ON `{tableName}`
FOR EACH ROW
BEGIN
    INSERT INTO {REWIND_TABLE_NAME} (`Command`) VALUES (CONCAT('DELETE FROM `{tableName}` WHERE ', $$COLUMN_VALUES$$, ';'));
END;";

        var colValues = string.Join(", ' AND ', ", columns
            .Select()
            .Select(x => GetColumnRepresentation(x))
            .Where(x => !string.IsNullOrWhiteSpace(x.columnName) && !x.generated)
            //.Select(x => $"'`{x.columnName}` = ', {x.template.Replace("$$alias$$","new")}"));
            .Select(x => $"'`{x.columnName}`', CASE WHEN new.`{x.columnName}` IS NULL THEN 'IS NULL' ELSE CONCAT(' = ', {x.template.Replace("$$alias$$", "new")}) END"));

        cmd.CommandText = template.Replace("$$COLUMN_VALUES$$", colValues);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task GenerateRewinderTable(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @$"
        CREATE TABLE `{REWIND_TABLE_NAME}` (
            Id BIGINT auto_increment NOT NULL,
            Command varchar(8000) NOT NULL,
            CONSTRAINT `__{REWIND_TABLE_NAME}_PK` PRIMARY KEY (Id)
        );";
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async ValueTask<int>  GenerateCascadeDeleteTriggers(DbConnection connection, DataTable columns)
    {
        var i = 0;
        var cmd = connection.CreateCommand();
        cmd.CommandText = @$"
        SELECT 
            CONSTRAINT_NAME, 
            TABLE_NAME 
        FROM 
            information_schema.REFERENTIAL_CONSTRAINTS 
        WHERE 
            DELETE_RULE = 'CASCADE' 
            AND CONSTRAINT_SCHEMA = '{connection.Database}';";

        var constraints = await cmd.ExecuteDataTableAsync().ConfigureAwait(false);

        foreach (var item in constraints.Select())
        {
            await GenerateCascadeDeleteTriggersForFK(connection, item["CONSTRAINT_NAME"]?.ToString() ?? string.Empty, ++i, columns).ConfigureAwait(false);
        }

        return i;
    }

    private static async Task GenerateCascadeDeleteTriggersForFK(DbConnection connection, string fkName, int i, DataTable columns)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @$"
            SELECT 
                TABLE_NAME,
                COLUMN_NAME,
                CONSTRAINT_NAME, 
                REFERENCED_TABLE_NAME,
                REFERENCED_COLUMN_NAME 
            FROM   
                INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
            WHERE CONSTRAINT_NAME = '{fkName}'
            AND CONSTRAINT_SCHEMA = '{connection.Database}';";

        var fkColumns = await cmd.ExecuteDataTableAsync().ConfigureAwait(false);

        var refTableName = fkColumns.Select().First()["REFERENCED_TABLE_NAME"].ToString();
        var tableName = fkColumns.Select().First()["TABLE_NAME"].ToString();

        var deleteWhere = string.Join(" AND ", fkColumns
            .Select()
            .Select(x => new {
                fk = x,
                refCol = columns.Select($"TABLE_NAME = '{refTableName}'").Single(z => z["COLUMN_NAME"].ToString() == x["REFERENCED_COLUMN_NAME"].ToString()),
                fkCol = columns.Select($"TABLE_NAME = '{tableName}'").Single(z => z["COLUMN_NAME"].ToString() == x["COLUMN_NAME"].ToString())
                })
            .Select(x => new
                {
                    fk = x.fk,
                    refCol = GetColumnRepresentation(x.refCol),
                    fkCol = GetColumnRepresentation(x.fkCol)
                })
            .Select(x => $"COALESCE(`{x.fkCol.columnName}`, 0) = COALESCE({x.refCol.template.Replace("$$alias$$", "old")}, 0)"));

        cmd.CommandText = $@"
            CREATE TRIGGER {REWIND_TRIGGER_NAME_PREFIX}_{i:D5}_BD_{fkName.WithMaxLength(32)} BEFORE DELETE ON `{refTableName}`
            FOR EACH ROW
            BEGIN
                DELETE FROM `{tableName}` WHERE {deleteWhere};
            END;";

        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static (string columnName, string template, bool generated) GetColumnRepresentation(DataRow columnSpecs)
    {
        string columnName = columnSpecs.Field<string>("COLUMN_NAME") ?? string.Empty;
        string dataType = columnSpecs.Field<string>("DATA_TYPE") ?? string.Empty;
        bool generated = !string.IsNullOrWhiteSpace(columnSpecs.Field<string>("GENERATION_EXPRESSION"));
        string result = $"QUOTE($$alias$$.`{columnName}`)";

        if (dataType == "decimal" || dataType == "double")
        {
            result = $"REGEXP_REPLACE(COALESCE($$alias$$.`{columnName}`, 'NULL'), '\\\\.?0+$', '')";
        }

        if (dataType == "bit")
        {
            result = $"CASE WHEN $$alias$$.`{columnName}` THEN 1 ELSE 0 END";
        }

        if (dataType == "text" || dataType == "longtext" || dataType == "varchar" || dataType == "char" || dataType == "longblob" || dataType == "json")
        {
            result = $"QUOTE(CAST($$alias$$.`{columnName}` AS CHAR CHARACTER SET utf8))";
        }

        return (columnName, result, generated);
    }

    
}
