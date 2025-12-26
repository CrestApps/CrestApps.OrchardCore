using CrestApps.OrchardCore.AI.Chat.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

internal sealed class AICustomChatInstanceMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AICustomChatInstanceIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("Source", column => column.WithLength(255))
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<string>("DisplayText", column => column.WithLength(255))
                .Column<DateTime>("CreatedUtc"),
            collection: AICustomChatConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AICustomChatInstanceIndex>(table => table
            .CreateIndex("IDX_AICustomChatInstanceIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Source",
                "UserId",
                "CreatedUtc",
                "DisplayText"),
            collection: AICustomChatConstants.CollectionName
        );

        return 1;
    }
}
