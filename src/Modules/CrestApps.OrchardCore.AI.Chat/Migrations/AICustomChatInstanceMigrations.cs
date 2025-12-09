using CrestApps.OrchardCore.AI.Chat.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

internal sealed class AICustomChatInstanceMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AICustomChatInstanceIndex>(table => table
                .Column<string>("InstanceId", column => column.WithLength(26))
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<string>("Title", column => column.WithLength(255))
                .Column<DateTime>("CreatedUtc"),
            collection: AICustomChatConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AICustomChatInstanceIndex>(table => table
            .CreateIndex("IDX_AICustomChatInstanceIndex_DocumentId",
                "DocumentId",
                "InstanceId",
                "UserId",
                "CreatedUtc",
                "Title"),
            collection: AICustomChatConstants.CollectionName
        );

        return 1;
    }
}
