using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Migrations;

/// <summary>
/// Defines the quiz part and the quiz course item type, and the grade scale index. Learning is new in 3.0.0, so no
/// stable release needs an upgrade: a site from a preview built before quizzes moved to Surveys is set up again.
/// </summary>
internal sealed class AssessmentsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="courseListSynchronizer">The course list synchronizer.</param>
    public AssessmentsMigrations(
        IContentDefinitionManager contentDefinitionManager,
        CourseListSynchronizer courseListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
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

        // Version 4 is the first one on Surveys: versions 1 to 3 were previews before quizzes moved to Surveys.
        return 4;
    }

    private Task DefineQuizPartAsync()
        => _contentDefinitionManager.AlterPartDefinitionAsync(nameof(QuizPart), part => part
            .Attachable(false)
            .WithDisplayName("Quiz")
            .WithDescription("Makes a course item a quiz whose questions, attempts and grading live in a scored survey of the Surveys module the quiz owns."));
}
