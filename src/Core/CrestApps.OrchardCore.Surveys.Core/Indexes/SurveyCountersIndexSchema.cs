using YesSql.Sql;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Creates the index table of survey counters.
/// </summary>
public static class SurveyCountersIndexSchema
{
    /// <summary>
    /// Creates the index table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = SurveysConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<SurveyCounterIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<string>("Kind", column => column.Nullable().WithLength(20)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyCounterIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyCounterIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyCounterIndex_Survey", "SurveyId", "Kind");
        }, collection: collection);
    }
}
