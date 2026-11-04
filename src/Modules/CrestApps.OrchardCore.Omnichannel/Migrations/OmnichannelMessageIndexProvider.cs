using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Migrations;

internal sealed class OmnichannelMessageIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("CustomerAddress", column => column.WithLength(255))
            .Column<string>("ServiceAddress", column => column.WithLength(255))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
            .Column<string>("ConversationId", column => column.WithLength(26))
            .Column<string>("ProviderMessageId", column => column.WithLength(128)),
            collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_DocumentId",
                "DocumentId",
                "Channel",
                "CustomerAddress",
                "ServiceAddress",
                "CreatedUtc"),
                collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_ConversationId",
                "DocumentId",
                "ConversationId",
                "CreatedUtc"),
                collection: OmnichannelConstants.CollectionName
        );

        await CreateProviderMessageIdIndexAsync();

        await CreateReportIndexAsync();

        await CreateLookupIndexesAsync();

        return 5;
    }

    /// <summary>
    /// Adds the <c>ConversationId</c> column and its index so the SMS portal can load a thread's message
    /// bubbles by conversation.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .AddColumn<string>("ConversationId", column => column.WithLength(26)),
            collection: OmnichannelConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_ConversationId",
                "DocumentId",
                "ConversationId",
                "CreatedUtc"),
                collection: OmnichannelConstants.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Adds the <c>ProviderMessageId</c> column and its index, so a delivery receipt matches the message it
    /// belongs to in one indexed seek instead of scanning a thread's outbound history, and a redelivered
    /// provider message is recognised as one already stored.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .AddColumn<string>("ProviderMessageId", column => column.WithLength(128)),
            collection: OmnichannelConstants.CollectionName
        );

        await CreateProviderMessageIdIndexAsync();

        return 3;
    }

    private Task CreateProviderMessageIdIndexAsync()
    {
        return SchemaBuilder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_Provider",
                "DocumentId",
                "Channel",
                "ProviderMessageId"),
                collection: OmnichannelConstants.CollectionName
        );
    }

    /// <summary>
    /// Adds the index reports read the records through, newest first by <c>CreatedUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await CreateReportIndexAsync();

        return 4;
    }

    /// <summary>
    /// Adds the indexes the message reads seek. Every earlier index leads with <c>DocumentId</c>, so a thread's
    /// messages, a delivery receipt's message and a channel's recent inbound or outbound messages were each found by
    /// reading the whole table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await CreateLookupIndexesAsync();

        return 5;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelMessageIndex>(
            SchemaBuilder,
            "IDX_OmnichannelMessageIndex_Report",
            OmnichannelConstants.CollectionName,
            "CreatedUtc", "Id");
    }

    private async Task CreateLookupIndexesAsync()
    {
        // A channel's messages in one direction, oldest first, which the channel's message lists page through.
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelMessageIndex>(
            SchemaBuilder,
            "IDX_OmnichannelMessageIndex_ChannelDirection",
            OmnichannelConstants.CollectionName,
            "Channel", "IsInbound", "CreatedUtc", "DocumentId");

        // The message a provider's delivery receipt or redelivery names.
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelMessageIndex>(
            SchemaBuilder,
            "IDX_OmnichannelMessageIndex_ProviderMessage",
            OmnichannelConstants.CollectionName,
            "Channel", "ProviderMessageId", "DocumentId");

        // A thread's messages in the order they were sent.
        await IndexTableIndexCreation.CreateIfMissingAsync<OmnichannelMessageIndex>(
            SchemaBuilder,
            "IDX_OmnichannelMessageIndex_Conversation",
            OmnichannelConstants.CollectionName,
            "ConversationId", "CreatedUtc", "DocumentId");
    }
}
