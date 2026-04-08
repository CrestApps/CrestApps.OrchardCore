using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.Indexing;

public static class AIDocumentChunkIndexSchemaBuilderExtensions
{
    public static async Task CreateAIDocumentChunkIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AIDocumentChunkIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AIDocumentId", column => column.WithLength(26))
            .Column<string>("ReferenceId", column => column.WithLength(26))
            .Column<string>("ReferenceType", column => column.WithLength(32))
            .Column<int>("Index"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIDocumentChunkIndex>(table => table
            .CreateIndex("IDX_AIDocumentChunkIndex_DocId",
                "DocumentId",
                "AIDocumentId",
                "ReferenceId",
                "ReferenceType"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIDocumentChunkIndex>(table => table
            .CreateIndex("IDX_AIDocumentChunkIndex_RefId",
                "AIDocumentId",
                "ReferenceId",
                "ReferenceType"),
            collection: collectionName);
    }
}
