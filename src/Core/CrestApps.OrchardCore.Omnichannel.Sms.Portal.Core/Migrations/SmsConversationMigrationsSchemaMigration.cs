using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Indexes;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Migrations;

/// <summary>
/// Creates the schema for the SMS Portal index tables (conversations, canned-response templates, and broadcasts).
/// </summary>
internal sealed class SmsConversationMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store, used to resolve the prefixed index table name.</param>
    /// <param name="logger">The logger.</param>
    public SmsConversationMigrationsSchemaMigration(
        IStore store,
        ILogger logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "SmsConversationMigrations";

    /// <summary>
    /// Creates the SMS Portal index tables.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<SmsConversationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ServiceAddress", column => column.WithLength(SmsPortalStorage.AddressLength))
            .Column<string>("ContactAddress", column => column.WithLength(SmsPortalStorage.AddressLength))
            .Column<string>("OwnerType", column => column.WithLength(32))
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<string>("AssignedAgentId", column => column.WithLength(26))
            .Column<string>("AssignmentStatus", column => column.WithLength(32))
            .Column<string>("Status", column => column.WithLength(32))
            .Column<bool>("IsRead")
            .Column<DateTime>("LastMessageUtc")
            .Column<int>("UnreadCount", column => column.NotNull().WithDefault(0))
            .Column<DateTime>("AssignedUtc", column => column.Nullable())
            .Column<DateTime>("FirstResponseDueUtc", column => column.Nullable()),
            collection: SmsPortalStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_Addresses",
                "DocumentId",
                "ServiceAddress",
                "ContactAddress"),
            collection: SmsPortalStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_Owner",
                "DocumentId",
                "OwnerType",
                "OwnerId",
                "AssignedAgentId",
                "LastMessageUtc"),
            collection: SmsPortalStorage.CollectionName
        );

        await CreateAddressesUniqueIndexAsync(builder);
        await CreatePickupIndexAsync(builder);
        await CreateFirstResponseIndexAsync(builder);

        await CreateTemplateTableAsync(builder);
        await CreateBroadcastTableAsync(builder);

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
                // Adds the unique index that keeps one conversation per number pair, so a concurrent create is
                // refused by the database rather than producing a second thread for the same contact.
                await CreateAddressesUniqueIndexAsync(builder);

                return 2;

            case 2:
                // Adds the 'UnreadCount' and 'AssignedUtc' columns and the pickup index, so the inbox badge and
                // the routed-pickup sweep are answered from the index instead of by loading and filtering every
                // thread.
                await builder.AlterIndexTableAsync<SmsConversationIndex>(table => table
                    .AddColumn<int>("UnreadCount", column => column.NotNull().WithDefault(0)),
                    collection: SmsPortalStorage.CollectionName
                );

                await builder.AlterIndexTableAsync<SmsConversationIndex>(table => table
                    .AddColumn<DateTime>("AssignedUtc", column => column.Nullable()),
                    collection: SmsPortalStorage.CollectionName
                );

                await CreatePickupIndexAsync(builder);

                return 3;

            case 3:
                // Adds the first-response deadline and the index the thirty-second sweep seeks on. Without the
                // index the sweep reads every open conversation on the tenant twice a minute.
                await builder.AlterIndexTableAsync<SmsConversationIndex>(table => table
                    .AddColumn<DateTime>("FirstResponseDueUtc", column => column.Nullable()),
                    collection: SmsPortalStorage.CollectionName
                );

                await CreateFirstResponseIndexAsync(builder);

                return 4;

            default:
                return version;
        }
    }

    private static Task CreateFirstResponseIndexAsync(ISchemaBuilder builder)
    {
        return builder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_FirstResponse",
                "DocumentId",
                "Status",
                "FirstResponseDueUtc"),
            collection: SmsPortalStorage.CollectionName
        );
    }

    private static Task CreatePickupIndexAsync(ISchemaBuilder builder)
    {
        return builder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_Pickup",
                "DocumentId",
                "OwnerType",
                "AssignmentStatus",
                "AssignedUtc"),
            collection: SmsPortalStorage.CollectionName
        );
    }

    private async Task CreateAddressesUniqueIndexAsync(ISchemaBuilder builder)
    {
        try
        {
            await SmsPortalMigrationSql.CreateUniqueIndexAsync(
                builder,
                _store,
                typeof(SmsConversationIndex),
                SmsPortalStorage.ConversationAddressesUniqueIndexName,
                "ServiceAddress",
                "ContactAddress");
        }
        catch (Exception ex)
        {
            // A tenant that already ran the workspace before the per-thread lock existed can hold duplicate
            // threads for one number pair, and the database refuses the constraint while they remain. Upgrading
            // must not fail on that: the lock is the primary defence and this index is defence in depth, so the
            // operator is told exactly what to merge rather than being blocked.
            _logger.LogError(
                ex,
                "Could not create the unique index '{IndexName}' on the SMS conversation table. Merge duplicate conversations that share a service address and contact address, then re-run the migration.",
                SmsPortalStorage.ConversationAddressesUniqueIndexName);
        }
    }

    private static async Task CreateTemplateTableAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<SmsTemplateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: SmsPortalStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SmsTemplateIndex>(table => table
            .CreateIndex("IDX_SmsTemplateIndex_Name",
                "DocumentId",
                "Name"),
            collection: SmsPortalStorage.CollectionName
        );
    }

    private static async Task CreateBroadcastTableAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<SmsBroadcastIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Status", column => column.WithLength(32)),
            collection: SmsPortalStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SmsBroadcastIndex>(table => table
            .CreateIndex("IDX_SmsBroadcastIndex_Status",
                "DocumentId",
                "Status"),
            collection: SmsPortalStorage.CollectionName
        );
    }
}
