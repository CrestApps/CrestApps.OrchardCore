using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Migrations;

/// <summary>
/// Creates the schema for the SMS Portal index tables (conversations, canned-response templates, and broadcasts).
/// </summary>
internal sealed class SmsConversationMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store, used to resolve the prefixed index table name.</param>
    /// <param name="logger">The logger.</param>
    public SmsConversationMigrations(
        IStore store,
        ILogger<SmsConversationMigrations> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Creates the SMS Portal index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SmsConversationIndex>(table => table
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

        await SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_Addresses",
                "DocumentId",
                "ServiceAddress",
                "ContactAddress"),
            collection: SmsPortalStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_Owner",
                "DocumentId",
                "OwnerType",
                "OwnerId",
                "AssignedAgentId",
                "LastMessageUtc"),
            collection: SmsPortalStorage.CollectionName
        );

        await CreateAddressesUniqueIndexAsync();
        await CreatePickupIndexAsync();
        await CreateFirstResponseIndexAsync();

        await CreateTemplateTableAsync();
        await CreateBroadcastTableAsync();

        return 4;
    }

    /// <summary>
    /// Adds the unique index that keeps one conversation per number pair, so a concurrent create is refused by
    /// the database rather than producing a second thread for the same contact.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateAddressesUniqueIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the <c>UnreadCount</c> and <c>AssignedUtc</c> columns and the pickup index, so the inbox badge and
    /// the routed-pickup sweep are answered from the index instead of by loading and filtering every thread.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .AddColumn<int>("UnreadCount", column => column.NotNull().WithDefault(0)),
            collection: SmsPortalStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .AddColumn<DateTime>("AssignedUtc", column => column.Nullable()),
            collection: SmsPortalStorage.CollectionName
        );

        await CreatePickupIndexAsync();

        return 3;
    }

    /// <summary>
    /// Adds the first-response deadline and the index the thirty-second sweep seeks on. Without the index the
    /// sweep reads every open conversation on the tenant twice a minute.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .AddColumn<DateTime>("FirstResponseDueUtc", column => column.Nullable()),
            collection: SmsPortalStorage.CollectionName
        );

        await CreateFirstResponseIndexAsync();

        return 4;
    }

    private Task CreateFirstResponseIndexAsync()
    {
        return SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_FirstResponse",
                "DocumentId",
                "Status",
                "FirstResponseDueUtc"),
            collection: SmsPortalStorage.CollectionName
        );
    }

    private Task CreatePickupIndexAsync()
    {
        return SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_Pickup",
                "DocumentId",
                "OwnerType",
                "AssignmentStatus",
                "AssignedUtc"),
            collection: SmsPortalStorage.CollectionName
        );
    }

    private async Task CreateAddressesUniqueIndexAsync()
    {
        try
        {
            await SmsPortalMigrationSql.CreateUniqueIndexAsync(
                SchemaBuilder,
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

    private async Task CreateTemplateTableAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SmsTemplateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: SmsPortalStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsTemplateIndex>(table => table
            .CreateIndex("IDX_SmsTemplateIndex_Name",
                "DocumentId",
                "Name"),
            collection: SmsPortalStorage.CollectionName
        );
    }

    private async Task CreateBroadcastTableAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SmsBroadcastIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Status", column => column.WithLength(32)),
            collection: SmsPortalStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsBroadcastIndex>(table => table
            .CreateIndex("IDX_SmsBroadcastIndex_Status",
                "DocumentId",
                "Status"),
            collection: SmsPortalStorage.CollectionName
        );
    }
}
