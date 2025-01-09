using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.OpenAI.Migrations;

internal sealed class OpenAIChatSessionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OpenAIChatSessionIndex>(table => table
                .Column<string>("ProfileId", column => column.WithLength(26))
                .Column<string>("SessionId", column => column.WithLength(26))
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<string>("ClientId", column => column.WithLength(64))
                .Column<DateTime>("CreatedUtc")
                .Column<string>("Title", column => column.WithLength(255)),
            collection: OpenAIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OpenAIChatSessionIndex>(table => table
            .CreateIndex("IDX_OpenAIChatSessionIndex_DocumentId",
                "DocumentId",
                "SessionId",
                "ProfileId",
                "UserId",
                "ClientId",
                "CreatedUtc",
                "Title"),
            collection: OpenAIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OpenAIChatSessionIndex>(table => table
            .CreateIndex("IDX_OpenAIChatSessionIndex_UserId",
                "DocumentId",
                "SessionId",
                "UserId",
                "CreatedUtc",
                "Title"),
            collection: OpenAIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<OpenAIChatSessionIndex>(table => table
            .CreateIndex("IDX_OpenAIChatSessionIndex_ClientId",
                "DocumentId",
                "SessionId",
                "ClientId",
                "CreatedUtc",
                "Title"),
            collection: OpenAIConstants.CollectionName
        );

        return 1;
    }

}
