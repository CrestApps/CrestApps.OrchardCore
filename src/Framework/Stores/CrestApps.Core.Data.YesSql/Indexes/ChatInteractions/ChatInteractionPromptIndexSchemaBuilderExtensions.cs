using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;

public static class ChatInteractionPromptIndexSchemaBuilderExtensions
{
    public static async Task CreateChatInteractionPromptIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<ChatInteractionPromptIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(64))
            .Column<string>("ChatInteractionId", column => column.WithLength(26))
            .Column<string>("Role", column => column.WithLength(20))
            .Column<DateTime>("CreatedUtc"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<ChatInteractionPromptIndex>(table => table
            .CreateIndex("IDX_ChatInteractionPromptIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ChatInteractionId"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<ChatInteractionPromptIndex>(table => table
            .CreateIndex("IDX_ChatInteractionPromptIndex_ChatInteractionId",
                "DocumentId",
                "ChatInteractionId",
                "CreatedUtc"),
            collection: collectionName);
    }
}
