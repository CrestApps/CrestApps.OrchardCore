using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;

public static class ChatInteractionIndexSchemaBuilderExtensions
{
    public static async Task CreateChatInteractionIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<ChatInteractionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("Title", column => column.WithLength(255))
            .Column<DateTime>("CreatedUtc"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<ChatInteractionIndex>(table => table
            .CreateIndex("IDX_ChatInteractionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "UserId",
                "Source",
                "Title",
                "CreatedUtc"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<ChatInteractionIndex>(table => table
            .CreateIndex("IDX_ChatInteractionIndex_UserId",
                "DocumentId",
                "UserId"),
            collection: collectionName);
    }
}
