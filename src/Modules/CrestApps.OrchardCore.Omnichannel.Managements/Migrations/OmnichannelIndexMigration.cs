using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;
using YesSql.Sql.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Provides a base class for Omnichannel index data migrations that must apply idempotent schema changes in
/// isolated transactions. Running each change on its own transaction and connection prevents a failure in one
/// migration from poisoning the shared migration session, which would otherwise roll back the schema changes
/// and version records of every sibling migration in the same feature.
/// </summary>
public abstract class OmnichannelIndexMigration : DataMigration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelIndexMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    protected OmnichannelIndexMigration(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger logger)
    {
        Store = store;
        DbConnectionAccessor = dbConnectionAccessor;
        Logger = logger;
    }

    /// <summary>
    /// Gets the YesSql store.
    /// </summary>
    protected IStore Store { get; }

    /// <summary>
    /// Gets the database connection accessor.
    /// </summary>
    protected IDbConnectionAccessor DbConnectionAccessor { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Applies a single schema change in its own isolated transaction on the supplied connection so a failure
    /// (most often because the target object already exists) cannot poison the shared migration transaction.
    /// </summary>
    /// <param name="connection">An open database connection used to run the isolated transaction.</param>
    /// <param name="schemaChange">The schema change to apply using an isolated <see cref="ISchemaBuilder"/>.</param>
    /// <param name="operation">A short description of the schema change used for diagnostic logging.</param>
    protected async Task ApplyIsolatedSchemaChangeAsync(
        DbConnection connection,
        Func<ISchemaBuilder, Task> schemaChange,
        string operation)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var schemaBuilder = new SchemaBuilder(Store.Configuration, transaction);
            await schemaChange(schemaBuilder);
            await transaction.CommitAsync();

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(
                    "Applied the isolated schema change to {SchemaChangeOperation}.",
                    operation);
            }
        }
        catch (Exception ex)
        {
            // Each idempotent change runs in its own transaction so a failure here (most often because the
            // object already exists) cannot poison the shared migration transaction. This is expected during
            // upgrades, so it is logged at Debug with the exception to keep normal upgrades quiet while still
            // preserving a full trace when production logging runs at Debug.
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(
                    ex,
                    "Skipped the isolated schema change to {SchemaChangeOperation} because it could not be applied; it most likely already exists.",
                    operation);
            }

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug(
                        rollbackException,
                        "Failed to roll back the isolated schema change transaction for the operation to {SchemaChangeOperation}.",
                        operation);
                }
            }
        }
    }

    /// <summary>
    /// Ensures a column exists on the map index table for <typeparamref name="TIndex"/> in the specified
    /// collection, adding it only when it is missing. Checking for the column first makes the change safe to
    /// run as a repair on databases whose migration version was recorded without the physical column, and a
    /// genuine failure while adding the column is allowed to propagate so the migration version is not
    /// advanced and the change is retried on the next start.
    /// </summary>
    /// <typeparam name="TIndex">The map index type whose table is altered.</typeparam>
    /// <param name="collection">The collection the index table belongs to.</param>
    /// <param name="columnName">The name of the column to ensure.</param>
    /// <param name="addColumn">Adds the column to the supplied table when it is missing.</param>
    /// <param name="operation">A short description of the schema change used for diagnostic logging.</param>
    protected async Task EnsureColumnExistsAsync<TIndex>(
        string collection,
        string columnName,
        Action<IAlterTableCommand> addColumn,
        string operation)
    {
        var tableName = Store.Configuration.TableNameConvention.GetIndexTable(typeof(TIndex), collection);
        var physicalTable = $"{Store.Configuration.TablePrefix}{tableName}";

        await using var connection = DbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        if (await ColumnExistsAsync(connection, physicalTable, columnName))
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(
                    "Skipped the schema change to {SchemaChangeOperation} because the '{ColumnName}' column already exists on the '{TableName}' table.",
                    operation,
                    columnName,
                    physicalTable);
            }

            return;
        }

        await using var transaction = await connection.BeginTransactionAsync();
        var schemaBuilder = new SchemaBuilder(Store.Configuration, transaction);
        await schemaBuilder.AlterIndexTableAsync<TIndex>(addColumn, collection: collection);
        await transaction.CommitAsync();

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation(
                "Applied the schema change to {SchemaChangeOperation} by adding the missing '{ColumnName}' column to the '{TableName}' table.",
                operation,
                columnName,
                physicalTable);
        }
    }

    /// <summary>
    /// Determines whether a column exists on the specified physical table using a dialect-appropriate lookup.
    /// </summary>
    /// <param name="connection">An open database connection used to run the lookup.</param>
    /// <param name="tableName">The physical, prefixed table name to inspect.</param>
    /// <param name="columnName">The name of the column to look for.</param>
    /// <returns><see langword="true"/> when the column exists; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string tableName,
        string columnName)
    {
        if (string.Equals(Store.Configuration.SqlDialect.Name, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var quotedTable = Store.Configuration.SqlDialect.QuoteForTableName(tableName, Store.Configuration.Schema);
            var columns = await connection.QueryAsync($"PRAGMA table_info({quotedTable})");

            return columns.Any(column => string.Equals((string)column.name, columnName, StringComparison.OrdinalIgnoreCase));
        }

        var schema = Store.Configuration.Schema;
        var sql = string.IsNullOrEmpty(schema)
            ? "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName"
            : "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";

        var count = await connection.ExecuteScalarAsync<int>(sql, new
        {
            Schema = schema,
            TableName = tableName,
            ColumnName = columnName,
        });

        return count > 0;
    }
}
