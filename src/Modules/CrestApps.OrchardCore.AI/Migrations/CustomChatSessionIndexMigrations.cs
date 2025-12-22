using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

public sealed class CustomChatSessionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CustomChatSessionIndex>(table => table
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("CustomChatInstanceId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<DateTime>("CreatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<CustomChatSessionIndex>(table => table
            .CreateIndex("IDX_CustomChatSessionIndex_DocumentId",
                "DocumentId",
                "SessionId",
                "CustomChatInstanceId",
                "UserId",
                "CreatedUtc",
                "DisplayText")
        );

        await SchemaBuilder.AlterIndexTableAsync<CustomChatSessionIndex>(table => table
            .CreateIndex("IDX_CustomChatSessionIndex_CustomChatInstanceId",
                "DocumentId",
                "CustomChatInstanceId",
                "CreatedUtc",
                "DisplayText")
        );

        await SchemaBuilder.AlterIndexTableAsync<CustomChatSessionIndex>(table => table
            .CreateIndex("IDX_CustomChatSessionIndex_UserId",
                "DocumentId",
                "SessionId",
                "UserId",
                "CreatedUtc",
                "DisplayText")
        );

        return 1;
    }
}
