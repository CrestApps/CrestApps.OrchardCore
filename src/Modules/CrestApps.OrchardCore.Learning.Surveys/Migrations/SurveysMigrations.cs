using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Surveys.Migrations;

/// <summary>
/// Creates the responses table, the survey part, the Feedback survey and Choice course item types, and adds them to the
/// course outlines. The types are defined in code: a recipe run inside a migration would wait forever on SQLite for
/// the write lock the migration holds.
/// </summary>
internal sealed class SurveysMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;

    public SurveysMigrations(IContentDefinitionManager contentDefinitionManager, CourseListSynchronizer courseListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
    }

    public async Task<int> CreateAsync()
    {
        await SurveysIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(SurveyPart), part => part
            .Attachable(false)
            .WithDisplayName("Survey")
            .WithDescription("Makes a course item a survey: its questions, anonymous answers, a close date and when learners see the totals."));

        await DefineAsync(SurveysConstants.ContentTypes.FeedbackSurvey, "Feedback survey", "Questions learners answer about the course, with the totals for the course staff.", singleQuestion: false);
        await DefineAsync(SurveysConstants.ContentTypes.Choice, "Choice", "One question learners answer by picking a choice, as a poll, with totals learners may see.", singleQuestion: true);

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
            .WithPart(nameof(SurveyPart), part => part
                .WithPosition("3")
                .WithSettings(new SurveyPartSettings { SingleQuestion = singleQuestion }))
            .WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = SurveysConstants.SurveyKind,
                    DefaultCompletionMode = CompletionModes.Submitted,
                })));
}
