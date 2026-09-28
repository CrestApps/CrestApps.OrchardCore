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
/// isolation. Isolating each change prevents a failure in one migration from poisoning the shared migration
/// session, which would otherwise roll back the schema changes and version records of every sibling migration in
/// the same feature.
/// </summary>
/// <remarks>
/// The host runs every step of a feature's migrations on one session transaction, exposed to the migration as
/// <see cref="DataMigration.SchemaBuilder"/>, so each change is isolated inside that transaction behind a
/// savepoint rather than on a second connection. A second connection waits for locks the host transaction already
/// holds and its own caller never releases: on SQLite that is the database's only write lock, taken by the first
/// step that writes, and on PostgreSQL and SQL Server it is the lock an earlier step's ALTER took on the same
/// table. The wait ends in a timeout that looks exactly like "the object already exists", so the change was
/// silently skipped, or startup stalled. MySQL is the exception: it commits DDL implicitly, which discards any
/// savepoint, so there each change still runs on its own connection, where no uncommitted DDL of the host can
/// block it.
/// </remarks>
public abstract class OmnichannelIndexMigration : DataMigration
{
    private const string SavepointName = "OmnichannelSchemaChange";

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
    /// Applies a single schema change in isolation so a failure (most often because the target object already
    /// exists) cannot poison the shared migration transaction.
    /// </summary>
    /// <param name="schemaChange">The schema change to apply using the supplied <see cref="ISchemaBuilder"/>.</param>
    /// <param name="operation">A short description of the schema change used for diagnostic logging.</param>
    protected async Task ApplyIsolatedSchemaChangeAsync(
        Func<ISchemaBuilder, Task> schemaChange,
        string operation)
    {
        var failure = await TryApplyIsolatedAsync(schemaChange);

        if (failure is null)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(
                    "Applied the isolated schema change to {SchemaChangeOperation}.",
                    operation);
            }

            return;
        }

        // A failure here is expected during upgrades (most often because the object already exists), so it is
        // logged at Debug with the exception to keep normal upgrades quiet while still preserving a full trace
        // when production logging runs at Debug.
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug(
                failure,
                "Skipped the isolated schema change to {SchemaChangeOperation} because it could not be applied; it most likely already exists.",
                operation);
        }
    }

    /// <summary>
    /// Runs database work in isolation and reports, rather than throws, the failure that undid it. The work must
    /// use the connection and transaction of the supplied <see cref="ISchemaBuilder"/>, raw SQL included, because
    /// any other connection can wait on the locks the host transaction holds.
    /// </summary>
    /// <param name="work">The work to run using the supplied <see cref="ISchemaBuilder"/>.</param>
    /// <returns>The exception that caused the work to be undone, or <see langword="null"/> when it was applied.</returns>
    protected Task<Exception> TryApplyIsolatedAsync(Func<ISchemaBuilder, Task> work)
    {
        var hostTransaction = GetSavepointCapableHostTransaction();

        return hostTransaction is null
            ? TryApplyOnSeparateConnectionAsync(work)
            : TryApplyInSavepointAsync(hostTransaction, work);
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
        var hostTransaction = GetSavepointCapableHostTransaction();
        bool added;

        if (hostTransaction is not null)
        {
            // The probe runs on the host transaction too, so it also sees a column an earlier step of the same run
            // added but has not committed yet; a probe on another connection could not.
            added = await AddColumnWhenMissingAsync<TIndex>(hostTransaction, collection, physicalTable, columnName, addColumn);
        }
        else
        {
            await using var connection = DbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            added = await AddColumnWhenMissingAsync<TIndex>(transaction, collection, physicalTable, columnName, addColumn);
            await transaction.CommitAsync();
        }

        if (!added)
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

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation(
                "Applied the schema change to {SchemaChangeOperation} by adding the missing '{ColumnName}' column to the '{TableName}' table.",
                operation,
                columnName,
                physicalTable);
        }
    }

    // A builder is created on the transaction rather than reusing the host's so a failed statement always throws:
    // a builder that swallowed the failure would release the savepoint over a change that never happened.
    private SchemaBuilder CreateSchemaBuilder(DbTransaction transaction)
        => new(Store.Configuration, transaction);

    // Returns the host transaction when a savepoint can isolate a change inside it, or null when the change has to
    // run on its own connection: outside the host (no transaction), on a provider without savepoints, or on MySQL,
    // whose implicit commit of DDL would discard the savepoint along with the isolation it provides.
    private DbTransaction GetSavepointCapableHostTransaction()
    {
        var transaction = SchemaBuilder?.Transaction;

        if (transaction?.Connection is null ||
            !transaction.SupportsSavepoints ||
            string.Equals(Store.Configuration.SqlDialect.Name, "MySql", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return transaction;
    }

    private async Task<Exception> TryApplyInSavepointAsync(DbTransaction transaction, Func<ISchemaBuilder, Task> work)
    {
        await transaction.SaveAsync(SavepointName);

        try
        {
            await work(CreateSchemaBuilder(transaction));
        }
        catch (Exception ex)
        {
            // Rolling back to the savepoint undoes only this change and, on PostgreSQL, clears the aborted state a
            // failed statement leaves the whole transaction in. It is deliberately not guarded: a transaction that
            // cannot return to the savepoint cannot run the sibling steps either, so that failure has to surface.
            await transaction.RollbackAsync(SavepointName);
            await ReleaseSavepointAsync(transaction);

            return ex;
        }

        await ReleaseSavepointAsync(transaction);

        return null;
    }

    // Releasing only keeps the savepoint stack flat; the change is already kept or undone by then. SQL Server has
    // no release statement, so a provider that reports the operation as unsupported leaves nothing to do.
    private static async Task ReleaseSavepointAsync(DbTransaction transaction)
    {
        try
        {
            await transaction.ReleaseAsync(SavepointName);
        }
        catch (NotSupportedException)
        {
        }
    }

    private async Task<Exception> TryApplyOnSeparateConnectionAsync(Func<ISchemaBuilder, Task> work)
    {
        try
        {
            await using var connection = DbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                await work(CreateSchemaBuilder(transaction));
                await transaction.CommitAsync();
            }
            catch
            {
                await TryRollbackAsync(transaction);

                throw;
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private async Task TryRollbackAsync(DbTransaction transaction)
    {
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
                    "Failed to roll back the transaction of an isolated schema change.");
            }
        }
    }

    private async Task<bool> AddColumnWhenMissingAsync<TIndex>(
        DbTransaction transaction,
        string collection,
        string tableName,
        string columnName,
        Action<IAlterTableCommand> addColumn)
    {
        if (await ColumnExistsAsync(transaction, tableName, columnName))
        {
            return false;
        }

        await CreateSchemaBuilder(transaction).AlterIndexTableAsync<TIndex>(addColumn, collection: collection);

        return true;
    }

    /// <summary>
    /// Determines whether a column exists on the specified physical table using a dialect-appropriate lookup.
    /// </summary>
    /// <param name="transaction">The transaction, and through it the connection, the lookup runs on.</param>
    /// <param name="tableName">The physical, prefixed table name to inspect.</param>
    /// <param name="columnName">The name of the column to look for.</param>
    /// <returns><see langword="true"/> when the column exists; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> ColumnExistsAsync(
        DbTransaction transaction,
        string tableName,
        string columnName)
    {
        var connection = transaction.Connection;

        if (string.Equals(Store.Configuration.SqlDialect.Name, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var quotedTable = Store.Configuration.SqlDialect.QuoteForTableName(tableName, Store.Configuration.Schema);
            var columns = await connection.QueryAsync($"PRAGMA table_info({quotedTable})", transaction: transaction);

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
        }, transaction);

        return count > 0;
    }
}
