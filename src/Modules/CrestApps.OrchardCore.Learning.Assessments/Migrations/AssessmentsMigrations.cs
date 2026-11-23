using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Migrations;

/// <summary>
/// Defines the quiz part and the quiz course item type, and the grade scale index. Version 4 upgrades a site from the
/// first Learning release (version 3), where quizzes drew content item questions from question banks.
/// </summary>
internal sealed class AssessmentsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;
    private readonly ResumableSteps _resumableSteps;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="courseListSynchronizer">The course list synchronizer.</param>
    /// <param name="resumableSteps">The resumable steps, which finish an upgrade in the background.</param>
    public AssessmentsMigrations(
        IContentDefinitionManager contentDefinitionManager,
        CourseListSynchronizer courseListSynchronizer,
        ResumableSteps resumableSteps)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
        _resumableSteps = resumableSteps;
    }

    /// <summary>
    /// Defines the part, the type and the index.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await AssessmentsIndexSchema.CreateAsync(SchemaBuilder);
        await DefineQuizPartAsync();

        await _contentDefinitionManager.AlterTypeDefinitionAsync(AssessmentsConstants.ContentTypes.Quiz, type => type
            .WithDisplayName("Quiz")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("2").WithEditor("Wysiwyg"))
            .WithPart(nameof(QuizPart), part => part.WithPosition("3"))
            .WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = AssessmentsConstants.QuizKind,
                    DefaultCompletionMode = CompletionModes.Passed,
                })));

        await _courseListSynchronizer.SynchronizeAsync();

        // Version 4 is the first one on Surveys: versions 1 to 3 were the first release, which UpdateFrom3Async upgrades.
        return 4;
    }

    /// <summary>
    /// Upgrades a site from the first Learning release: removes the question bank, the 13 question types, their parts,
    /// questions and the attempt tables, and gives every quiz a scored survey with its pass mark, time limit, attempt
    /// limit and window. The questions are not carried over: instructors add them again in each quiz's survey.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        // The index tables of the first release's attempts, attempt items, overrides, question sources and attempt
        // deadlines. Learning is new in 3.0.0, so no stable release created them.
        await SchemaBuilder.DropTableAsync("Learning_AssessmentAttemptIndex");
        await SchemaBuilder.DropTableAsync("Learning_AttemptItemIndex");
        await SchemaBuilder.DropTableAsync("Learning_AssessmentOverrideIndex");
        await SchemaBuilder.DropTableAsync("QuestionSourceIndex");
        await SchemaBuilder.DropTableAsync("LearningAttemptDeadlines_LeasedWorkItemIndex");

        // The items of the removed types are deleted, and the quizzes given their surveys, in the background a batch at a
        // time (LegacyQuizUpgradeStep), so the migration never reads every item at once.
        foreach (var type in LegacyQuizUpgrade.RemovedContentTypes)
        {
            if (await _contentDefinitionManager.GetTypeDefinitionAsync(type) is not null)
            {
                await _contentDefinitionManager.DeleteTypeDefinitionAsync(type);
            }
        }

        foreach (var part in LegacyQuizUpgrade.RemovedContentParts)
        {
            if (await _contentDefinitionManager.GetPartDefinitionAsync(part) is not null)
            {
                await _contentDefinitionManager.DeletePartDefinitionAsync(part);
            }
        }

        await DefineQuizPartAsync();
        await _resumableSteps.RequestAsync(LegacyQuizUpgradeStep.StepName);

        return 4;
    }

    private Task DefineQuizPartAsync()
        => _contentDefinitionManager.AlterPartDefinitionAsync(nameof(QuizPart), part => part
            .Attachable(false)
            .WithDisplayName("Quiz")
            .WithDescription("Makes a course item a quiz whose questions, attempts and grading live in a scored survey of the Surveys module the quiz owns."));
}
