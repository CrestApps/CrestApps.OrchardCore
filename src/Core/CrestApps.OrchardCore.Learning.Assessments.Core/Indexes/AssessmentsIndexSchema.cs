using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;

/// <summary>
/// Creates the index tables of Learning Assessments.
/// </summary>
public static class AssessmentsIndexSchema
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = LearningConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<GradeScaleIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<GradeScaleIndex>(table =>
            table.CreateIndex("IDX_Learning_GradeScaleIndex_Name", "Name"), collection: collection);
    }
}
