using CrestApps.OrchardCore.AI.Chat.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

public sealed class CustomChatPartMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CustomChatPartIndex>(table => table
            .Column<string>("ContentItemId", c => c.WithLength(26))
            .Column<string>("CustomChatInstanceId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("ProviderName", column => column.WithLength(255))
            .Column<string>("ConnectionName", column => column.WithLength(255))
            .Column<string>("DeploymentId", column => column.WithLength(255))
            .Column<string>("DisplayText", c => c.WithLength(255))
            .Column<bool>("IsCustomInstance")
            .Column<bool>("UseCaching")
            .Column<DateTime>("CreatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<CustomChatPartIndex>(table => table
            .CreateIndex(
            "IDX_CustomChatPartIndex_ContentItemId",
            "ContentItemId",
            "CustomChatInstanceId",
            "SessionId",
            "UserId",
            "IsCustomInstance",
            "CreatedUtc",
            "DisplayText")
        );

        return 1;
    }
}
