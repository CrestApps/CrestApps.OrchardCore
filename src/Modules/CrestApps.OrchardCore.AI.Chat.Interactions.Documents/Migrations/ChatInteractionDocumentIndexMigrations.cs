using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Indexes;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Migrations;

internal sealed class ChatInteractionDocumentIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ChatInteractionDocumentIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(64))
                .Column<string>("ChatInteractionId", column => column.WithLength(26))
                .Column<string>("Extension", column => column.WithLength(20)),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ChatInteractionDocumentIndex>(table => table
            .CreateIndex("IDX_ChatInteractionDocumentIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ChatInteractionId",
                "Extension"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ChatInteractionDocumentIndex>(table => table
            .CreateIndex("IDX_ChatInteractionDocumentIndex_ChatInteractionId",
                "DocumentId",
                "ChatInteractionId"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
