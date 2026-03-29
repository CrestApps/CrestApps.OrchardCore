using CrestApps.OrchardCore.AI.Memory.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Memory.Migrations;

internal sealed class AIMemoryEntryMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIMemoryEntryIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<string>("Name", column => column.WithLength(256))
                .Column<string>("NormalizedName", column => column.WithLength(256))
                .Column<DateTime>("CreatedUtc")
                .Column<DateTime>("UpdatedUtc"),
            collection: MemoryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table => table
            .CreateIndex(
                "IDX_AIMemoryEntryIndex_ItemId",
                "DocumentId",
                "ItemId"),
            collection: MemoryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table => table
            .CreateIndex(
                "IDX_AIMemoryEntryIndex_User_UpdatedUtc",
                "DocumentId",
                "UserId",
                "UpdatedUtc"),
            collection: MemoryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table => table
            .CreateIndex(
                "IDX_AIMemoryEntryIndex_User_NormalizedName",
                "DocumentId",
                "UserId",
                "NormalizedName"),
            collection: MemoryConstants.CollectionName
        );

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table =>
        {
            table.AddColumn<string>("Name", column => column.WithLength(256));
            table.AddColumn<string>("NormalizedName", column => column.WithLength(256));
        },
            collection: MemoryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIMemoryEntryIndex>(table => table
            .CreateIndex(
                "IDX_AIMemoryEntryIndex_User_NormalizedName",
                "DocumentId",
                "UserId",
                "NormalizedName"),
            collection: MemoryConstants.CollectionName
        );

        return 2;
    }
}
