using System.Data.Common;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

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
}
