using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIChatSessionIndex>(table => table
                .Column<string>("ProfileId", column => column.WithLength(26))
                .Column<string>("SessionId", column => column.WithLength(26))
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<string>("ClientId", column => column.WithLength(64))
                .Column<DateTime>("CreatedUtc")
                .Column<string>("Title", column => column.WithLength(255)),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_DocumentId",
                "DocumentId",
                "SessionId",
                "ProfileId",
                "UserId",
                "ClientId",
                "CreatedUtc",
                "Title"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_UserId",
                "DocumentId",
                "SessionId",
                "UserId",
                "CreatedUtc",
                "Title"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_ClientId",
                "DocumentId",
                "SessionId",
                "ClientId",
                "CreatedUtc",
                "Title"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
