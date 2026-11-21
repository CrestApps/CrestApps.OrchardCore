using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Surveys.Migrations;

/// <summary>
/// Defines the course survey part and the feedback survey and choice course item types.
/// </summary>
internal sealed class LearningSurveysMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveysMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="courseListSynchronizer">The course list synchronizer.</param>
    public LearningSurveysMigrations(IContentDefinitionManager contentDefinitionManager, CourseListSynchronizer courseListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
    }

    /// <summary>
    /// Defines the part and the types.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CourseSurveyPart), part => part
            .Attachable(false)
            .WithDisplayName("Course survey")
            .WithDescription("Makes a course item a survey whose questions live in a survey of the Surveys module the item owns."));

        await DefineAsync(LearningSurveysConstants.ContentTypes.FeedbackSurvey, "Feedback survey", "Questions learners answer about the course, with the totals for the course staff.", singleQuestion: false);
        await DefineAsync(LearningSurveysConstants.ContentTypes.Choice, "Choice", "One question learners answer by picking a choice, as a poll, with totals learners may see.", singleQuestion: true);

        await _courseListSynchronizer.SynchronizeAsync();

        return 1;
    }

    private Task DefineAsync(string typeName, string displayName, string description, bool singleQuestion)
        => _contentDefinitionManager.AlterTypeDefinitionAsync(typeName, type => type
            .WithDisplayName(displayName)
            .WithDescription(description)
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("2").WithEditor("Wysiwyg").WithDisplayName("Introduction"))
            .WithPart(nameof(CourseSurveyPart), part => part
                .WithPosition("3")
                .WithSettings(new CourseSurveyPartSettings { SingleQuestion = singleQuestion }))
            .WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = LearningSurveysConstants.SurveyKind,
                    DefaultCompletionMode = CompletionModes.Submitted,
                })));
}
