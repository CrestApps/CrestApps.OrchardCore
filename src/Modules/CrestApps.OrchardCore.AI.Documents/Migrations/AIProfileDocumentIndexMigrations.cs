using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIProfileDocumentIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIProfileDocumentIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(64))
                .Column<string>("ProfileId", column => column.WithLength(26))
                .Column<string>("Extension", column => column.WithLength(20)),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIProfileDocumentIndex>(table => table
            .CreateIndex("IDX_AIProfileDocumentIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ProfileId",
                "Extension"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIProfileDocumentIndex>(table => table
            .CreateIndex("IDX_AIProfileDocumentIndex_ProfileId",
                "DocumentId",
                "ProfileId"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
