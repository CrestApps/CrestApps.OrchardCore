using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.OpenAI.Migrations;

internal sealed class AIChatSessionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIChatSessionIndex>(table => table
                .Column<string>("ProfileId", column => column.WithLength(26))
                .Column<string>("SessionId", column => column.WithLength(26))
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<string>("ClientId", column => column.WithLength(64))
                .Column<DateTime>("CreatedAtUtc")
                .Column<string>("Title", column => column.WithLength(255)),
            collection: OpenAIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_DocumentId",
                "DocumentId",
                "SessionId",
                "ProfileId",
                "UserId",
                "ClientId",
                "CreatedAtUtc",
                "Title"),
            collection: OpenAIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_UserId",
                "DocumentId",
                "SessionId",
                "UserId",
                "CreatedAtUtc",
                "Title"),
            collection: OpenAIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table => table
            .CreateIndex("IDX_AIChatSessionIndex_ClientId",
                "DocumentId",
                "SessionId",
                "ClientId",
                "CreatedAtUtc",
                "Title"),
            collection: OpenAIConstants.CollectionName
        );

        return 1;
    }

}
