using System.Data.Common;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Migrations;

internal sealed class OmnichannelContactCommunicationPreferenceIndexMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactCommunicationPreferenceIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelContactCommunicationPreferenceIndexMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<OmnichannelContactCommunicationPreferenceIndexMigrations> logger)
    {
        _store = store;
        _dbConnectionAccessor = dbConnectionAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await EnsureDefaultContactCommunicationPreferenceIndexTableAsync();

        return 3;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await EnsureDefaultContactCommunicationPreferenceIndexTableAsync();

        return 2;
    }

    /// <summary>
    /// Removes the chat preference columns, which no channel could ever act on.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        // The chat preference was writable but never readable: the platform has no chat channel, no processor for
        // one, and no path that can create chat work, so a contact who asked not to be chatted with was told
        // something the product could not honour. The promise is withdrawn, so the columns behind it go too.
        await using var connection = _dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        await ApplyIsolatedSchemaChangeAsync(connection,
            builder => builder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table =>
                table.DropColumn("DoNotChat")),
            "drop the obsolete 'DoNotChat' column");

        await ApplyIsolatedSchemaChangeAsync(connection,
            builder => builder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table =>
                table.DropColumn("DoNotChatUtc")),
            "drop the obsolete 'DoNotChatUtc' column");

        return 3;
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

    private async Task EnsureDefaultContactCommunicationPreferenceIndexTableAsync()
    {
        try
        {
            await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
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
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
                .CreateIndex("IDX_OmnichannelContactCommunicationPreferenceIndex_DoNotCallUtc",
                    "DocumentId",
                    "DoNotCallUtc"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OmnichannelContactCommunicationPreferenceIndex_DoNotCallUtc' index may already exist on the default-collection OmnichannelContactCommunicationPreferenceIndex table.");
        }
    }
}
