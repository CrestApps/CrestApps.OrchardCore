using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;

internal sealed class ChatInteractionMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ChatInteractionIndex>(table => table
                .Column<string>("InteractionId", column => column.WithLength(26))
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<string>("Title", column => column.WithLength(255))
                .Column<DateTime>("CreatedUtc")
                .Column<DateTime>("ModifiedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ChatInteractionIndex>(table => table
            .CreateIndex("IDX_ChatInteractionIndex_DocumentId",
                "DocumentId",
                "InteractionId",
                "UserId",
                "Title",
                "CreatedUtc",
                "ModifiedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ChatInteractionIndex>(table => table
            .CreateIndex("IDX_ChatInteractionIndex_UserId",
                "DocumentId",
                "UserId",
                "ModifiedUtc"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
