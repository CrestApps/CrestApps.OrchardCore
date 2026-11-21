using YesSql.Sql;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Creates the answer index table.
/// </summary>
public static class SurveyAnswerIndexSchema
{
    /// <summary>
    /// Creates the answer index table and its database indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = SurveysConstants.ResponsesCollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<SurveyAnswerIndex>(table => table
            .Column<string>("ResponseId", column => column.Nullable().WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<int>("VersionNumber")
            .Column<string>("QuestionId", column => column.Nullable().WithLength(64))
            .Column<string>("QuestionName", column => column.Nullable().WithLength(100))
            .Column<string>("RowId", column => column.Nullable().WithLength(64))
            .Column<string>("ChoiceId", column => column.Nullable().WithLength(64))
            .Column<decimal>("NumberValue", column => column.Nullable())
            .Column<DateTime?>("DateValue", column => column.Nullable())
            .Column<string>("TextValue", column => column.Nullable().WithLength(SurveyAnswerIndex.TextLength))
            .Column<decimal>("Score", column => column.Nullable())
            .Column<decimal>("MaxPoints", column => column.Nullable())
            .Column<DateTime>("CompletedUtc")
            .Column<string>("CollectorId", column => column.Nullable().WithLength(26))
            .Column<string>("ContextKind", column => column.Nullable().WithLength(100))
            .Column<string>("ContextRef", column => column.Nullable().WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyAnswerIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyAnswerIndex_Choice", "SurveyId", "QuestionId", "ChoiceId");
            table.CreateIndex("IDX_SurveyAnswerIndex_Completed", "SurveyId", "CompletedUtc");
            table.CreateIndex("IDX_SurveyAnswerIndex_Response", "ResponseId");
        }, collection: collection);
    }
}
