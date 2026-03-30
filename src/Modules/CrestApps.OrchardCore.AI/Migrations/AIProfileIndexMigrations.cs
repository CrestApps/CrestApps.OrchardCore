using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIProfileIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIProfileIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("Name", column => column.WithLength(255))
                .Column<string>("Description", column => column.Nullable().WithLength(500))
                .Column<string>("Source", column => column.WithLength(255))
                .Column<string>("Type", column => column.WithLength(50))
                .Column<string>("ConnectionName", column => column.WithLength(255))
                .Column<string>("DeploymentId", column => column.WithLength(255))
                .Column<string>("DeploymentName", column => column.WithLength(255))
                .Column<string>("OrchestratorName", column => column.WithLength(255))
                .Column<string>("OwnerId", column => column.WithLength(26))
                .Column<string>("Author", column => column.WithLength(255))
                .Column<bool>("IsListable"),
            collection: AIConstants.AICollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .CreateIndex("IDX_AIProfileIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Name",
                "Source",
                "Type"),
            collection: AIConstants.AICollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .CreateIndex("IDX_AIProfileIndex_Type",
                "DocumentId",
                "Type",
                "IsListable",
                "Name"),
            collection: AIConstants.AICollectionName
        );

        return 3;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .AddColumn<string>("Description", column => column.Nullable().WithLength(500)),
            collection: AIConstants.AICollectionName
        );

        return 2;
    }

    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .AddColumn<string>("DeploymentName", column => column.WithLength(255)),
            collection: AIConstants.AICollectionName
        );

        return 3;
    }
}
