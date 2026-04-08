using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.Indexing;

public static class AIDocumentIndexSchemaBuilderExtensions
{
    public static async Task CreateAIDocumentIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AIDocumentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(64))
            .Column<string>("ReferenceId", column => column.WithLength(64))
            .Column<string>("ReferenceType", column => column.WithLength(32))
            .Column<string>("Extension", column => column.WithLength(20)),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIDocumentIndex>(table => table
            .CreateIndex("IDX_AIDocumentIndex_ItemId",
                "DocumentId",
                "ItemId",
                "ReferenceId",
                "ReferenceType",
                "Extension"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIDocumentIndex>(table => table
            .CreateIndex("IDX_AIDocumentIndex_ReferenceId",
                "DocumentId",
                "ReferenceId",
                "ReferenceType"),
            collection: collectionName);
    }
}
