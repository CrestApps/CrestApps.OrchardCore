using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Surveys.Core.Indexes;

/// <summary>
/// Indexes survey responses by learner, enrollment, survey, course and date.
/// </summary>
public sealed class SurveyResponseIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the enrollment.</summary>
    public string EnrollmentId { get; set; }

    /// <summary>Gets or sets the learner.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the course.</summary>
    public string CourseId { get; set; }

    /// <summary>Gets or sets the survey item.</summary>
    public string SurveyId { get; set; }

    /// <summary>Gets or sets when the learner first answered, in UTC.</summary>
    public DateTime SubmittedUtc { get; set; }
}

/// <summary>
/// Maps survey responses.
/// </summary>
public sealed class SurveyResponseIndexProvider : IndexProvider<SurveyResponse>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseIndexProvider"/> class.
    /// </summary>
    public SurveyResponseIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyResponse> context)
        => context.For<SurveyResponseIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(response => new SurveyResponseIndex
            {
                ItemId = response.ItemId,
                EnrollmentId = response.EnrollmentId,
                UserId = response.UserId,
                CourseId = response.CourseId,
                SurveyId = response.SurveyId,
                SubmittedUtc = response.SubmittedUtc,
            });
}

/// <summary>
/// Creates the index table of survey responses.
/// </summary>
public static class SurveysIndexSchema
{
    /// <summary>
    /// Creates the table and its database indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = SurveysConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<SurveyResponseIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EnrollmentId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CourseId", column => column.Nullable().WithLength(26))
            .Column<string>("SurveyId", column => column.WithLength(26))
            .Column<DateTime>("SubmittedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyResponseIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_SurveyResponseIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Learning_SurveyResponseIndex_Enrollment", "EnrollmentId", "SurveyId");
            table.CreateIndex("IDX_Learning_SurveyResponseIndex_Survey", "SurveyId", "SubmittedUtc");
            table.CreateIndex("IDX_Learning_SurveyResponseIndex_User", "UserId");
        }, collection: collection);
    }
}
