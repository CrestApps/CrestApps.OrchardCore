using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionPromptIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIChatSessionPromptIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(64))
                .Column<string>("SessionId", column => column.WithLength(26))
                .Column<string>("Role", column => column.WithLength(20))
                .Column<DateTime>("CreatedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionPromptIndex>(table => table
            .CreateIndex("IDX_AIChatSessionPromptIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "SessionId"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionPromptIndex>(table => table
            .CreateIndex("IDX_AIChatSessionPromptIndex_SessionId",
                "DocumentId",
                "SessionId",
                "CreatedUtc"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
