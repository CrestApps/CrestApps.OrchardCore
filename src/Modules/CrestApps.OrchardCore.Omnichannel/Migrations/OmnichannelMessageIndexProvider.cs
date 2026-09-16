using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
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

        return 3;
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
}
