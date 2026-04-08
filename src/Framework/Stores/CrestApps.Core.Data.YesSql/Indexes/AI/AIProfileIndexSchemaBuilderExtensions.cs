using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.AI;

public static class AIProfileIndexSchemaBuilderExtensions
{
    public static async Task CreateAIProfileIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AIProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Description", column => column.Nullable().WithLength(500))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("Type", column => column.WithLength(50))
            .Column<string>("ConnectionName", column => column.WithLength(255))
            .Column<string>("DeploymentId", column => column.Nullable().WithLength(255))
            .Column<string>("DeploymentName", column => column.Nullable().WithLength(255))
            .Column<string>("OrchestratorName", column => column.WithLength(255))
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<string>("Author", column => column.WithLength(255))
            .Column<bool>("IsListable"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .CreateIndex("IDX_AIProfileIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Name",
                "Source",
                "Type"),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .CreateIndex("IDX_AIProfileIndex_Type",
                "DocumentId",
                "Type",
                "IsListable",
                "Name"),
            collection: collectionName);
    }

    public static Task AddAIProfileIndexDescriptionColumnAsync(this ISchemaBuilder schemaBuilder, string collectionName = null) =>
        schemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .AddColumn<string>("Description", column => column.Nullable().WithLength(500)),
            collection: collectionName);

    public static Task AddAIProfileIndexDeploymentNameColumnAsync(this ISchemaBuilder schemaBuilder, string collectionName = null) =>
        schemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
            .AddColumn<string>("DeploymentName", column => column.Nullable().WithLength(255)),
            collection: collectionName);
}
