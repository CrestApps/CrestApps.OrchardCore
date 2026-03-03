using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Indexes;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;

internal sealed class ChatInteractionPromptIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ChatInteractionPromptIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(64))
                .Column<string>("ChatInteractionId", column => column.WithLength(26))
                .Column<string>("Role", column => column.WithLength(20))
                .Column<DateTime>("CreatedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ChatInteractionPromptIndex>(table => table
            .CreateIndex("IDX_ChatInteractionPromptIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ChatInteractionId"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ChatInteractionPromptIndex>(table => table
            .CreateIndex("IDX_ChatInteractionPromptIndex_ChatInteractionId",
                "DocumentId",
                "ChatInteractionId",
                "CreatedUtc"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
