using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using CrestApps.Core.Omnichannel;
using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Omnichannel.Migrations;

/// <summary>
/// Creates the schema for the <see cref="OmnichannelMessageIndex"/>.
/// </summary>
public sealed class OmnichannelMessageIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "OmnichannelMessageIndexMigrations";

    /// <summary>
    /// Creates the message index table and its supporting indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<OmnichannelMessageIndex>(table => table
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("CustomerAddress", column => column.WithLength(255))
            .Column<string>("ServiceAddress", column => column.WithLength(255))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<bool>("IsInbound", column => column.NotNull().WithDefault(false))
            .Column<string>("ConversationId", column => column.WithLength(26))
            .Column<string>("ProviderMessageId", column => column.WithLength(128)),
            collection: OmnichannelCollections.Name
        );

        await builder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_DocumentId",
                "DocumentId",
                "Channel",
                "CustomerAddress",
                "ServiceAddress",
                "CreatedUtc"),
                collection: OmnichannelCollections.Name
        );

        await builder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_ConversationId",
                "DocumentId",
                "ConversationId",
                "CreatedUtc"),
                collection: OmnichannelCollections.Name
        );

        await CreateProviderMessageIdIndexAsync(builder);

        return 3;
    }

    /// <inheritdoc/>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                return await UpdateFrom1Async(builder);

            case 2:
                return await UpdateFrom2Async(builder);

            default:
                return version;
        }
    }

    /// <summary>
    /// Adds the <c>ConversationId</c> column and its index so the SMS portal can load a thread's message
    /// bubbles by conversation.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes steps per method.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    private static async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
        await builder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .AddColumn<string>("ConversationId", column => column.WithLength(26)),
            collection: OmnichannelCollections.Name
        );

        await builder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_ConversationId",
                "DocumentId",
                "ConversationId",
                "CreatedUtc"),
                collection: OmnichannelCollections.Name
        );

        return 2;
    }

    /// <summary>
    /// Adds the <c>ProviderMessageId</c> column and its index, so a delivery receipt matches the message it
    /// belongs to in one indexed seek instead of scanning a thread's outbound history, and a redelivered
    /// provider message is recognised as one already stored.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes steps per method.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    private static async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
        await builder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .AddColumn<string>("ProviderMessageId", column => column.WithLength(128)),
            collection: OmnichannelCollections.Name
        );

        await CreateProviderMessageIdIndexAsync(builder);

        return 3;
    }

    private static Task CreateProviderMessageIdIndexAsync(ISchemaBuilder builder)
    {
        return builder.AlterIndexTableAsync<OmnichannelMessageIndex>(table => table
            .CreateIndex("IDX_OmnichannelMessageIndex_Provider",
                "DocumentId",
                "Channel",
                "ProviderMessageId"),
                collection: OmnichannelCollections.Name
        );
    }
}
