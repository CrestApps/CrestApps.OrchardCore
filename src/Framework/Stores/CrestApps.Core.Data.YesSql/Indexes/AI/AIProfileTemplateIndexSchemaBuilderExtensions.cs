using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.AI;

public static class AIProfileTemplateIndexSchemaBuilderExtensions
{
    public static async Task CreateAIProfileTemplateIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AIProfileTemplateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Category", column => column.WithLength(255))
            .Column<string>("ProfileType", column => column.WithLength(50))
            .Column<bool>("IsListable"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIProfileTemplateIndex>(table => table
            .CreateIndex("IDX_AIProfileTemplateIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Source",
                "Name"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIProfileTemplateIndex>(table => table
            .CreateIndex("IDX_AIProfileTemplateIndex_Listable",
                "DocumentId",
                "IsListable",
                "Source",
                "Category",
                "Name"),
            collection: collectionName);
    }

    public static Task AddAIProfileTemplateIndexSourceColumnAsync(this ISchemaBuilder schemaBuilder, string collectionName = null) =>
        schemaBuilder.AlterIndexTableAsync<AIProfileTemplateIndex>(table => table
            .AddColumn<string>("Source", column => column.WithLength(255)),
            collection: collectionName);
}
