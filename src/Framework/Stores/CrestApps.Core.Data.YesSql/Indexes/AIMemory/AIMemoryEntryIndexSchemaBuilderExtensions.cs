using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.AIMemory;

public static class AIMemoryEntryIndexSchemaBuilderExtensions
{
    public static async Task CreateAIMemoryEntryIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AIMemoryEntryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(256))
            .Column<string>("NormalizedName", column => column.WithLength(256))
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("UpdatedUtc"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table => table
            .CreateIndex(
                "IDX_AIMemoryEntryIndex_ItemId",
                "DocumentId",
                "ItemId"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table => table
            .CreateIndex(
                "IDX_AIMemoryEntryIndex_User_UpdatedUtc",
                "DocumentId",
                "UserId",
                "UpdatedUtc"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table => table
            .CreateIndex(
                "IDX_AIMemoryEntryIndex_User_NormalizedName",
                "DocumentId",
                "UserId",
                "NormalizedName"),
            collection: collectionName);
    }

    public static async Task AddAIMemoryEntryNameColumnsAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table =>
        {
            table.AddColumn<string>("Name", column => column.WithLength(256));
            table.AddColumn<string>("NormalizedName", column => column.WithLength(256));
        }, collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table => table
            .CreateIndex(
                "IDX_AIMemoryEntryIndex_User_NormalizedName",
                "DocumentId",
                "UserId",
                "NormalizedName"),
            collection: collectionName);
    }
}
