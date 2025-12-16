using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class CustomChatSessionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AICustomChatSessionIndex>(table => table
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("CustomChatInstanceId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<DateTime>("CreatedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AICustomChatSessionIndex>(table => table
            .CreateIndex(
                "IDX_AICustomChatSessionIndex_DocumentId",
                "DocumentId",
                "SessionId",
                "CustomChatInstanceId",
                "UserId",
                "CreatedUtc",
                "DisplayText"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AICustomChatSessionIndex>(table => table
            .CreateIndex(
                "IDX_AICustomChatSessionIndex_CustomChatInstanceId",
                "DocumentId",
                "CustomChatInstanceId",
                "CreatedUtc",
                "DisplayText"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AICustomChatSessionIndex>(table => table
            .CreateIndex(
                "IDX_AICustomChatSessionIndex_UserId",
                "DocumentId",
                "SessionId",
                "UserId",
                "CreatedUtc",
                "DisplayText"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
