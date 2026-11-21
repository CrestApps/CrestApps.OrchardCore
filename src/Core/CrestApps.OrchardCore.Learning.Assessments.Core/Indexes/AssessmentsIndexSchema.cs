using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;

/// <summary>
/// Creates the index tables of assessments.
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

        await schemaBuilder.CreateMapIndexTableAsync<AssessmentAttemptIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("QuizId", column => column.WithLength(26))
            .Column<string>("CourseId", column => column.Nullable().WithLength(26))
            .Column<string>("EnrollmentId", column => column.Nullable().WithLength(26))
            .Column<string>("Number", column => column.Nullable().WithLength(32))
            .Column<AttemptStatus>("Status")
            .Column<bool>("NeedsGrading")
            .Column<DateTime>("StartedUtc")
            .Column<DateTime>("DeadlineUtc", column => column.Nullable())
            .Column<DateTime>("SubmittedUtc", column => column.Nullable())
            .Column<decimal>("Percent", column => column.Nullable())
            .Column<bool>("Passed", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AssessmentAttemptIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_AttemptIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Learning_AttemptIndex_UserQuiz", "UserId", "QuizId", "Status");
            table.CreateIndex("IDX_Learning_AttemptIndex_Quiz", "QuizId", "Status");
            table.CreateIndex("IDX_Learning_AttemptIndex_Grading", "NeedsGrading", "SubmittedUtc");
            table.CreateIndex("IDX_Learning_AttemptIndex_SubmittedUtc", "SubmittedUtc", "Id");
            table.CreateIndex("IDX_Learning_AttemptIndex_StartedUtc", "StartedUtc", "Id");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AttemptItemIndex>(table => table
            .Column<string>("AttemptId", column => column.WithLength(26))
            .Column<string>("QuizId", column => column.WithLength(26))
            .Column<string>("QuestionId", column => column.Nullable().WithLength(26))
            .Column<string>("QuestionType", column => column.Nullable().WithLength(64))
            .Column<decimal>("Score", column => column.Nullable())
            .Column<decimal>("Points")
            .Column<decimal>("AttemptPercent", column => column.Nullable())
            .Column<DateTime>("SubmittedUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AttemptItemIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_AttemptItemIndex_Quiz", "QuizId", "QuestionId");
            table.CreateIndex("IDX_Learning_AttemptItemIndex_SubmittedUtc", "SubmittedUtc", "Id");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AssessmentOverrideIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("QuizId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AssessmentOverrideIndex>(table =>
            table.CreateIndex("IDX_Learning_OverrideIndex_UserQuiz", "UserId", "QuizId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<GradeScaleIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<GradeScaleIndex>(table =>
            table.CreateIndex("IDX_Learning_GradeScaleIndex_Name", "Name"), collection: collection);

        await LeasedWorkItemIndexSchema.CreateAsync(schemaBuilder, AttemptDeadlineWork.CollectionName);
    }
}
