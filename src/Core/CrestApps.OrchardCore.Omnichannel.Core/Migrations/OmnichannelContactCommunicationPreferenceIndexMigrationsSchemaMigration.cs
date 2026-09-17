using System.Data.Common;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Core.Migrations;

/// <summary>
/// Creates the schema for the contact communication preference index, which records the channels a contact
/// has asked not to be reached on.
/// </summary>
internal sealed class OmnichannelContactCommunicationPreferenceIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="OmnichannelContactCommunicationPreferenceIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelContactCommunicationPreferenceIndexMigrationsSchemaMigration(
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
    public string Name => "OmnichannelContactCommunicationPreferenceIndexMigrations";

    /// <summary>
    /// Creates the contact communication preference index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await EnsureDefaultContactCommunicationPreferenceIndexTableAsync(builder);

        return 3;
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
                await EnsureDefaultContactCommunicationPreferenceIndexTableAsync(builder);

                return 2;

            case 2:
                return await UpdateFrom2Async(builder);

            default:
                return version;
        }
    }

    // Each drop runs on its own transaction because a tenant created after the columns were removed never had them,
    // and a failed drop on the shared migration transaction would roll back every sibling migration in the feature.
    private async Task ApplyIsolatedSchemaChangeAsync(
        DbConnection connection,
        Func<ISchemaBuilder, Task> schemaChange,
        string operation)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await schemaChange(new SchemaBuilder(_store.Configuration, transaction));
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            // A tenant whose table was created without these columns fails here, which is the expected outcome rather
            // than a fault, so it stays at Debug where a trace is still available when production logs at that level.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Skipped the isolated schema change to {SchemaChangeOperation}; the object it targets is most likely already absent.", operation);
            }

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(rollbackException, "Failed to roll back the isolated schema change transaction for the operation to {SchemaChangeOperation}.", operation);
                }
            }
        }
    }

    private async Task EnsureDefaultContactCommunicationPreferenceIndexTableAsync(ISchemaBuilder builder)
    {
        try
        {
            await builder.CreateMapIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<bool>("DoNotCall", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotCallUtc")
                .Column<bool>("DoNotSms", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotSmsUtc")
                .Column<bool>("DoNotEmail", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotEmailUtc")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The default-collection OmnichannelContactCommunicationPreferenceIndex table may already exist.");
        }

        try
        {
            await builder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
                .CreateIndex("IDX_OmnichannelContactCommunicationPreferenceIndex_DoNotCallUtc",
                    "DocumentId",
                    "DoNotCallUtc"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OmnichannelContactCommunicationPreferenceIndex_DoNotCallUtc' index may already exist on the default-collection OmnichannelContactCommunicationPreferenceIndex table.");
        }
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
                    // Removes the chat preference columns, which no channel could ever act on.
                    //
                    // The chat preference was writable but never readable: the platform has no chat channel, no processor for
                    // one, and no path that can create chat work, so a contact who asked not to be chatted with was told
                    // something the product could not honour. The promise is withdrawn, so the columns behind it go too.
                    await using var connection = _dbConnectionAccessor.CreateConnection();
                    await connection.OpenAsync();

                    await ApplyIsolatedSchemaChangeAsync(connection,
                        isolatedBuilder => isolatedBuilder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table =>
                            table.DropColumn("DoNotChat")),
                        "drop the obsolete 'DoNotChat' column");

                    await ApplyIsolatedSchemaChangeAsync(connection,
                        isolatedBuilder => isolatedBuilder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table =>
                            table.DropColumn("DoNotChatUtc")),
                        "drop the obsolete 'DoNotChatUtc' column");

                    return 3;
                }
}
