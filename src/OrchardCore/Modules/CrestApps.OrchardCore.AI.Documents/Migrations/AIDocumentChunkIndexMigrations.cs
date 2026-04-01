using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Documents.Migrations;

internal sealed class AIDocumentChunkIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIDocumentChunkIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AIDocumentId", column => column.WithLength(26))
            .Column<string>("ReferenceId", column => column.WithLength(26))
            .Column<string>("ReferenceType", column => column.WithLength(32))
            .Column<int>("Index"),
        collection: AIConstants.AIDocsCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIDocumentChunkIndex>(table => table
            .CreateIndex("IDX_AIDocumentChunkIndex_DocId",
        "DocumentId",
        "AIDocumentId",
        "ReferenceId",
        "ReferenceType"),
        collection: AIConstants.AIDocsCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIDocumentChunkIndex>(table => table
            .CreateIndex("IDX_AIDocumentChunkIndex_RefId",
        "AIDocumentId",
        "ReferenceId",
        "ReferenceType"),
        collection: AIConstants.AIDocsCollectionName
        );

        return 1;
    }
}
