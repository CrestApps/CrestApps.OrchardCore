using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql.Sql;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;

/// <summary>
/// Creates the schema for the messaging workspace index tables (conversations, canned-response templates, and
/// broadcasts), and imports what the SMS-only portal that preceded the workspace had stored.
/// </summary>
internal sealed class MessagingMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store, used to resolve the prefixed index table name.</param>
    /// <param name="logger">The logger.</param>
    public MessagingMigrations(
        IStore store,
        ILogger<MessagingMigrations> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Creates the workspace index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<MessagingConversationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(MessagingStorage.ChannelLength))
            .Column<string>("ServiceAddress", column => column.WithLength(MessagingStorage.AddressLength))
            .Column<string>("ContactAddress", column => column.WithLength(MessagingStorage.AddressLength))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("CustomerKey", column => column.WithLength(MessagingStorage.CustomerKeyLength))
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
            collection: MessagingStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MessagingConversationIndex>(table => table
            .CreateIndex("IDX_MessagingConversationIndex_Addresses",
                "DocumentId",
                "Channel",
                "ServiceAddress",
                "ContactAddress"),
            collection: MessagingStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MessagingConversationIndex>(table => table
            .CreateIndex("IDX_MessagingConversationIndex_Customer",
                "DocumentId",
                "CustomerKey",
                "LastMessageUtc"),
            collection: MessagingStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MessagingConversationIndex>(table => table
            .CreateIndex("IDX_MessagingConversationIndex_Owner",
                "DocumentId",
                "OwnerType",
                "OwnerId",
                "AssignedAgentId",
                "LastMessageUtc"),
            collection: MessagingStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MessagingConversationIndex>(table => table
            .CreateIndex("IDX_MessagingConversationIndex_Pickup",
                "DocumentId",
                "OwnerType",
                "AssignmentStatus",
                "AssignedUtc"),
            collection: MessagingStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MessagingConversationIndex>(table => table
            .CreateIndex("IDX_MessagingConversationIndex_FirstResponse",
                "DocumentId",
                "Status",
                "FirstResponseDueUtc"),
            collection: MessagingStorage.CollectionName
        );

        await CreateAddressesUniqueIndexAsync();

        await SchemaBuilder.CreateMapIndexTableAsync<MessageTemplateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: MessagingStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MessageTemplateIndex>(table => table
            .CreateIndex("IDX_MessageTemplateIndex_Name",
                "DocumentId",
                "Name"),
            collection: MessagingStorage.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<MessagingBroadcastIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Status", column => column.WithLength(32)),
            collection: MessagingStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MessagingBroadcastIndex>(table => table
            .CreateIndex("IDX_MessagingBroadcastIndex_Status",
                "DocumentId",
                "Status"),
            collection: MessagingStorage.CollectionName
        );

        // The import reads and writes documents, which needs the tables this step has just created to be committed.
        // Running it inside the migration's own transaction would also mean opening a second connection mid-step,
        // which deadlocks SQLite.
        ShellScope.AddDeferredTask(scope => LegacySmsPortalImport.ImportAsync(scope.ServiceProvider));

        await SchemaBuilder.AlterIndexTableAsync<MessageTemplateIndex>(table => table
            .CreateIndex(
                "IDX_MessageTemplateIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: MessagingStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<MessagingBroadcastIndex>(table => table
            .CreateIndex(
                "IDX_MessagingBroadcastIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: MessagingStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<MessagingConversationIndex>(table => table
            .CreateIndex(
                "IDX_MessagingConversationIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: MessagingStorage.CollectionName);

        await CreateReportIndexAsync();

        return 3;
    }

    private async Task CreateAddressesUniqueIndexAsync()
    {
        try
        {
            await MessagingMigrationSql.CreateUniqueIndexAsync(
                SchemaBuilder,
                _store,
                typeof(MessagingConversationIndex),
                MessagingStorage.ConversationAddressesUniqueIndexName,
                "Channel",
                "ServiceAddress",
                "ContactAddress");
        }
        catch (Exception ex)
        {
            // The per-thread lock is the primary defence and this index is defence in depth, so a database that
            // refuses the constraint must not fail the feature's activation: the operator is told what to do instead.
            _logger.LogError(
                ex,
                "Could not create the unique index '{IndexName}' on the messaging conversation table. Merge duplicate conversations that share a channel, service address and contact address, then re-run the migration.",
                MessagingStorage.ConversationAddressesUniqueIndexName);
        }
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<MessageTemplateIndex>(
            SchemaBuilder,
            "IDX_MessageTemplateIndex_ItemId",
            MessagingStorage.CollectionName,
            "ItemId", "DocumentId");

        await IndexTableIndexCreation.CreateIfMissingAsync<MessagingBroadcastIndex>(
            SchemaBuilder,
            "IDX_MessagingBroadcastIndex_ItemId",
            MessagingStorage.CollectionName,
            "ItemId", "DocumentId");

        await IndexTableIndexCreation.CreateIfMissingAsync<MessagingConversationIndex>(
            SchemaBuilder,
            "IDX_MessagingConversationIndex_ItemId",
            MessagingStorage.CollectionName,
            "ItemId", "DocumentId");

        return 2;
    }

    /// <summary>
    /// Adds the index reports read the records through, newest first by <c>LastMessageUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await CreateReportIndexAsync();

        return 3;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<MessagingConversationIndex>(
            SchemaBuilder,
            "IDX_MessagingConversationIndex_Report",
            MessagingStorage.CollectionName,
            "LastMessageUtc", "Id");
    }
}
