using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIProfileTemplateIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIProfileTemplateIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("Source", column => column.WithLength(255))
                .Column<string>("Name", column => column.WithLength(255))
                .Column<string>("Category", column => column.WithLength(255))
                .Column<string>("ProfileType", column => column.WithLength(50))
                .Column<bool>("IsListable"),
            collection: AIConstants.AICollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIProfileTemplateIndex>(table => table
            .CreateIndex("IDX_AIProfileTemplateIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Source",
                "Name"),
            collection: AIConstants.AICollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIProfileTemplateIndex>(table => table
            .CreateIndex("IDX_AIProfileTemplateIndex_Listable",
                "DocumentId",
                "IsListable",
                "Source",
                "Category",
                "Name"),
            collection: AIConstants.AICollectionName
        );

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AIProfileTemplateIndex>(table => table
            .AddColumn<string>("Source", column => column.WithLength(255)),
            collection: AIConstants.AICollectionName
        );

        return 2;
    }
}
