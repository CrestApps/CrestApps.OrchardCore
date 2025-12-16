using CrestApps.OrchardCore.AI.Chat.Indexes;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

internal sealed class CustomChatPartMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CustomChatPartIndex>(table => table
            .Column<string>("CustomChatInstanceId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("ProviderName", column => column.WithLength(255))
            .Column<string>("ConnectionName", column => column.WithLength(255))
            .Column<string>("DeploymentId", column => column.WithLength(255))
            .Column<string>("Title", column => column.WithLength(255))
            .Column<bool>("IsCustomInstance")
            .Column<bool>("UseCaching")
            .Column<DateTime>("CreatedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CustomChatPartIndex>(table => table
            .CreateIndex(
                "IDX_CustomChatPartIndex_DocumentId",
                "DocumentId",
                "CustomChatInstanceId",
                "SessionId",
                "UserId",
                "IsCustomInstance",
                "CreatedUtc",
                "Title"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
