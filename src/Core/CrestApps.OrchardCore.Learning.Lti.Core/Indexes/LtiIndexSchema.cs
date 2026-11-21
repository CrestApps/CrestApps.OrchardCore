using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Indexes;

/// <summary>
/// Creates the index tables of External Tools in their own collection.
/// </summary>
public static class LtiIndexSchema
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = LtiConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<ExternalToolIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("ClientId", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ExternalToolIndex>(table =>
            table.CreateIndex("IDX_LearningLti_ToolIndex_ClientId", "ClientId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<LtiLineItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ToolId", column => column.Nullable().WithLength(26))
            .Column<string>("CourseId", column => column.Nullable().WithLength(26))
            .Column<string>("ResourceLinkId", column => column.Nullable().WithLength(26))
            .Column<string>("ResourceId", column => column.Nullable().WithLength(255))
            .Column<string>("Tag", column => column.Nullable().WithLength(255))
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LtiLineItemIndex>(table =>
            table.CreateIndex("IDX_LearningLti_LineItemIndex_Course", "CourseId", "ResourceLinkId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<LtiScoreIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("LineItemId", column => column.Nullable().WithLength(26))
            .Column<string>("CourseId", column => column.Nullable().WithLength(26))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("ToolId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LtiScoreIndex>(table =>
            table.CreateIndex("IDX_LearningLti_ScoreIndex_LineItemUser", "LineItemId", "UserId"), collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LtiScoreIndex>(table =>
            table.CreateIndex("IDX_LearningLti_ScoreIndex_User", "UserId"), collection: collection);
    }
}
