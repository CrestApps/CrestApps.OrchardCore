using CrestApps.Core.Omnichannel.Models;
using System.Data.Common;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Dapper;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql;
using YesSql.Sql;
using YesSql.Sql.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Core.Migrations;

/// <summary>
/// Creates and repairs the schema for the omnichannel activity batch index table.
/// </summary>
/// <remarks>
/// Several steps apply their change on an isolated connection and transaction rather than on the migration's
/// own builder. Running each change on its own transaction prevents a failure in one migration from poisoning
/// the shared migration session, which would otherwise roll back the schema changes and version records of
/// every sibling migration in the same feature.
/// </remarks>
internal sealed class OmnichannelActivityBatchIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelActivityBatchIndexMigrationsSchemaMigration(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger logger)
    {
        _store = store;
        _dbConnectionAccessor = dbConnectionAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "OmnichannelActivityBatchIndexMigrations";

    /// <summary>
    /// Creates the omnichannel activity batch index table with the final set of columns and indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<string>("Source", column => column.WithLength(50))
            .Column<OmnichannelActivityBatchStatus>("Status")
            .Column<DateTime>("CreatedUtc"),
        collection: OmnichannelConstants.CollectionName
        );

        // This SQL index is for locating incoming message from Omnichannel (Incoming SMS, Email, etc).
        await builder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityBatchIndex_DocumentId",
        "DocumentId",
        "DisplayText",
        "ItemId"
        ),
        collection: OmnichannelConstants.CollectionName
        );

        return 4;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                return await UpdateFrom1Async(builder);

            case 2:
                return await UpdateFrom2Async(builder);

            case 3:
                // Repairs databases whose migration version was recorded at or above the step that added the
                // 'Source' and 'CreatedUtc' columns while the physical columns were rolled back with a failed
                // sibling migration. Because the earlier version-gated steps no longer run on those databases,
                // this step verifies each column and adds only the ones that are missing, so the activity
                // batches screen can order by 'CreatedUtc' again.
                await EnsureColumnExistsAsync<OmnichannelActivityBatchIndex>(
                    OmnichannelConstants.CollectionName,
                    "Source",
                    table => table.AddColumn<string>("Source", column => column.WithLength(50)),
                    "ensure the 'Source' column exists on the omnichannel activity batch index");

                await EnsureColumnExistsAsync<OmnichannelActivityBatchIndex>(
                    OmnichannelConstants.CollectionName,
                    "CreatedUtc",
                    table => table.AddColumn<DateTime>("CreatedUtc"),
                    "ensure the 'CreatedUtc' column exists on the omnichannel activity batch index");

                return 4;

            default:
                return version;
        }
    }

    /// <summary>
    /// Applies a single schema change in its own isolated transaction on the supplied connection so a failure
    /// (most often because the target object already exists) cannot poison the shared migration transaction.
    /// </summary>
    /// <param name="connection">An open database connection used to run the isolated transaction.</param>
    /// <param name="schemaChange">The schema change to apply using an isolated <see cref="ISchemaBuilder"/>.</param>
    /// <param name="operation">A short description of the schema change used for diagnostic logging.</param>
    private async Task ApplyIsolatedSchemaChangeAsync(
        DbConnection connection,
        Func<ISchemaBuilder, Task> schemaChange,
        string operation)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);
            await schemaChange(schemaBuilder);
            await transaction.CommitAsync();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
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
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
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
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
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
    private async Task EnsureColumnExistsAsync<TIndex>(
        string collection,
        string columnName,
        Action<IAlterTableCommand> addColumn,
        string operation)
    {
        var tableName = _store.Configuration.TableNameConvention.GetIndexTable(typeof(TIndex), collection);
        var physicalTable = $"{_store.Configuration.TablePrefix}{tableName}";

        await using var connection = _dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        if (await ColumnExistsAsync(connection, physicalTable, columnName))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped the schema change to {SchemaChangeOperation} because the '{ColumnName}' column already exists on the '{TableName}' table.",
                    operation,
                    columnName,
                    physicalTable);
            }

            return;
        }

        await using var transaction = await connection.BeginTransactionAsync();
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);
        await schemaBuilder.AlterIndexTableAsync<TIndex>(addColumn, collection: collection);
        await transaction.CommitAsync();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
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
        if (string.Equals(_store.Configuration.SqlDialect.Name, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var quotedTable = _store.Configuration.SqlDialect.QuoteForTableName(tableName, _store.Configuration.Schema);
            var columns = await connection.QueryAsync($"PRAGMA table_info({quotedTable})");

            return columns.Any(column => string.Equals((string)column.name, columnName, StringComparison.OrdinalIgnoreCase));
        }

        var schema = _store.Configuration.Schema;
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

    /// <summary>
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds the activity batch source column in an isolated transaction so it survives sibling
                    // migration failures.
                    await using var connection = _dbConnectionAccessor.CreateConnection();
                    await connection.OpenAsync();

                    await ApplyIsolatedSchemaChangeAsync(connection,
                        isolatedBuilder => isolatedBuilder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table =>
                            table.AddColumn<string>("Source", column => column.WithLength(50)),
                            collection: OmnichannelConstants.CollectionName),
                        "add the 'Source' column to the omnichannel activity batch index");

                    return 2;
                }

    /// <summary>
    /// Moves the schema from version 2 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the activity batch created UTC column, used to order batches by newest first, in an
                    // isolated transaction so it survives sibling migration failures in the same feature.
                    await using var connection = _dbConnectionAccessor.CreateConnection();
                    await connection.OpenAsync();

                    await ApplyIsolatedSchemaChangeAsync(connection,
                        isolatedBuilder => isolatedBuilder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table =>
                            table.AddColumn<DateTime>("CreatedUtc"),
                            collection: OmnichannelConstants.CollectionName),
                        "add the 'CreatedUtc' column to the omnichannel activity batch index");

                    return 3;
                }
}
