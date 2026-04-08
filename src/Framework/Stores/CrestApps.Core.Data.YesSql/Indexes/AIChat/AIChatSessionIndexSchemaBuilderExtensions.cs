using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

public static class AIChatSessionIndexSchemaBuilderExtensions
{
    public static async Task CreateAIChatSessionIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AIChatSessionIndex>(table => table
            .Column<string>("ProfileId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("ClientId", column => column.WithLength(64))
            .Column<string>("Status", column => column.WithDefault("Active"))
            .Column<string>("PostSessionProcessingStatus", column => column.WithDefault("None"))
            .Column<DateTime>("LastActivityUtc")
            .Column<DateTime>("CreatedUtc")
            .Column<string>("Title", column => column.WithLength(255)),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_DocumentId",
                "DocumentId",
                "SessionId",
                "ProfileId",
                "UserId",
                "ClientId",
                "CreatedUtc",
                "Title"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_UserId",
                "DocumentId",
                "SessionId",
                "UserId",
                "CreatedUtc",
                "Title"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_ClientId",
                "DocumentId",
                "SessionId",
                "ClientId",
                "CreatedUtc",
                "Title"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_ProfileStatusLastActivityUtc",
                "DocumentId",
                "ProfileId",
                "Status",
                "LastActivityUtc"),
            collection: collectionName);
    }

    public static async Task AddAIChatSessionIndexStatusColumnsAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table =>
        {
            table.AddColumn<string>("Status", column => column.WithDefault("Active"));
            table.AddColumn<DateTime>("LastActivityUtc");
        }, collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_ProfileStatusLastActivityUtc",
                "DocumentId",
                "ProfileId",
                "Status",
                "LastActivityUtc"),
            collection: collectionName);
    }

    public static Task AddAIChatSessionIndexPostSessionProcessingStatusColumnAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        return schemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table =>
        {
            table.AddColumn<string>("PostSessionProcessingStatus", column => column.WithDefault("None"));
        }, collection: collectionName);
    }
}
