using System.Data.Common;
using YesSql.Sql;
using YesSql.Sql.Schema;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Adds a column to an index table unless a column of that name is already there.
/// </summary>
/// <remarks>
/// An upgrade step that adds a column fails when the column exists, and that failure cancels every migration still
/// pending in the activation. The reverse also happens: a database can have the step's version recorded while the
/// column is missing, and then every save of a document that maps to the index fails with "Invalid column name".
/// Checking first lets a step add a column it needs whatever state it finds, so it can also serve as a repair.
/// </remarks>
public static class IndexTableColumnCreation
{
    /// <summary>
    /// Adds a column to the index table of <typeparamref name="TIndex"/> when it is missing.
    /// </summary>
    /// <typeparam name="TIndex">The index whose table carries the column.</typeparam>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="collection">The collection the index table belongs to; <see langword="null"/> for the default one.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="addColumn">Adds the column to the table, for example <c>table =&gt; table.AddColumn&lt;string&gt;(...)</c>.</param>
    /// <returns><see langword="true"/> when the column was added; <see langword="false"/> when it was already there.</returns>
    public static Task<bool> AddIfMissingAsync<TIndex>(
        ISchemaBuilder schemaBuilder,
        string collection,
        string columnName,
        Action<IAlterTableCommand> addColumn)
        => AddIfMissingAsync(schemaBuilder, typeof(TIndex), collection, columnName, addColumn);

    /// <summary>
    /// Adds a column to the index table of <paramref name="indexType"/> when it is missing.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="indexType">The index whose table carries the column.</param>
    /// <param name="collection">The collection the index table belongs to; <see langword="null"/> for the default one.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="addColumn">Adds the column to the table.</param>
    /// <returns><see langword="true"/> when the column was added; <see langword="false"/> when it was already there.</returns>
    public static async Task<bool> AddIfMissingAsync(
        ISchemaBuilder schemaBuilder,
        Type indexType,
        string collection,
        string columnName,
        Action<IAlterTableCommand> addColumn)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(indexType);
        ArgumentException.ThrowIfNullOrEmpty(columnName);
        ArgumentNullException.ThrowIfNull(addColumn);

        if (await ExistsAsync(schemaBuilder, indexType, collection, columnName))
        {
            return false;
        }

        await schemaBuilder.AlterIndexTableAsync(indexType, addColumn, collection);

        return true;
    }

    /// <summary>
    /// Determines whether the index table of <paramref name="indexType"/> has a column of the given name.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="indexType">The index whose table is checked.</param>
    /// <param name="collection">The collection the index table belongs to; <see langword="null"/> for the default one.</param>
    /// <param name="columnName">The name of the column.</param>
    /// <returns><see langword="true"/> when the column exists.</returns>
    public static async Task<bool> ExistsAsync(ISchemaBuilder schemaBuilder, Type indexType, string collection, string columnName)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(indexType);
        ArgumentException.ThrowIfNullOrEmpty(columnName);

        var tableName = schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(indexType, collection);

        // Every query reads only the catalog and runs on the migration's own transaction, so it sees a column an
        // earlier step of the same run added, and needs no second connection that could wait on that step's locks.
        var sql = schemaBuilder.Dialect.Name switch
        {
            "SqlServer" =>
                "SELECT COUNT(1) FROM sys.columns c INNER JOIN sys.tables t ON t.object_id = c.object_id " +
                "WHERE c.name = @column AND t.name = @table",
            "PostgreSql" =>
                "SELECT COUNT(1) FROM information_schema.columns WHERE column_name = @column AND table_name = @table",
            "MySql" =>
                "SELECT COUNT(1) FROM information_schema.columns " +
                "WHERE column_name = @column AND table_name = @table AND table_schema = DATABASE()",
            _ =>
                "SELECT COUNT(1) FROM pragma_table_info(@table) WHERE name = @column",
        };

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = sql;
        AddParameter(command, "@column", columnName);
        AddParameter(command, "@table", tableName);

        var result = await command.ExecuteScalarAsync();

        return result is not null and not DBNull &&
            Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
