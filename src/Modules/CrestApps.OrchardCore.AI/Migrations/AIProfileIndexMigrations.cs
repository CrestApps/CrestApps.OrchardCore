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
                .Column<string>("DisplayText", column => column.WithLength(255))
                .Column<string>("Source", column => column.WithLength(255))
                .Column<string>("Type", column => column.WithLength(50))
                .Column<string>("ConnectionName", column => column.WithLength(255))
                .Column<string>("DeploymentId", column => column.WithLength(255))
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
                "DisplayText",
                "Source",
                "Type"),
            collection: AIConstants.AICollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .CreateIndex("IDX_AIProfileIndex_Type",
                "DocumentId",
                "Type",
                "IsListable",
                "DisplayText"),
            collection: AIConstants.AICollectionName
        );

        return 1;
    }
}
