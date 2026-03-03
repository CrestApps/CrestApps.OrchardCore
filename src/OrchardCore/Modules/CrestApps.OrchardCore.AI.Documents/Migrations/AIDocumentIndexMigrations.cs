using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIDocumentIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIDocumentIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(64))
                .Column<string>("ReferenceId", column => column.WithLength(64))
                .Column<string>("ReferenceType", column => column.WithLength(32))
                .Column<string>("Extension", column => column.WithLength(20)),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIDocumentIndex>(table => table
            .CreateIndex("IDX_AIDocumentIndex_ItemId",
                "DocumentId",
                "ItemId",
                "ReferenceId",
                "ReferenceType",
                "Extension"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIDocumentIndex>(table => table
            .CreateIndex("IDX_AIDocumentIndex_ReferenceId",
                "DocumentId",
                "ReferenceId",
                "ReferenceType"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
