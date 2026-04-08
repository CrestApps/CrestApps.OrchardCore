using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

public static class AIChatSessionPromptIndexSchemaBuilderExtensions
{
    public static async Task CreateAIChatSessionPromptIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AIChatSessionPromptIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(64))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("Role", column => column.WithLength(20))
            .Column<DateTime>("CreatedUtc"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionPromptIndex>(table => table
            .CreateIndex("IDX_AIChatSessionPromptIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "SessionId"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionPromptIndex>(table => table
            .CreateIndex("IDX_AIChatSessionPromptIndex_SessionId",
                "DocumentId",
                "SessionId",
                "CreatedUtc"),
            collection: collectionName);
    }
}
