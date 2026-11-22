using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Modules;
using OrchardCore.Title.Models;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Assessments.Migrations;

/// <summary>
/// Defines the quiz part and the quiz course item type, and the grade scale index. Version 4 upgrades a site from the
/// first Learning release (version 3), where quizzes drew content item questions from question banks.
/// </summary>
internal sealed class AssessmentsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;
    private readonly ISession _session;
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ISurveyManager _surveyManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="courseListSynchronizer">The course list synchronizer.</param>
    /// <param name="session">The session.</param>
    /// <param name="learningSurveys">The Learning survey service, which creates the surveys of upgraded quizzes.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="logger">The logger.</param>
    public AssessmentsMigrations(
        IContentDefinitionManager contentDefinitionManager,
        CourseListSynchronizer courseListSynchronizer,
        ISession session,
        ILearningSurveyService learningSurveys,
        ISurveyManager surveyManager,
        ILogger<AssessmentsMigrations> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
        _session = session;
        _learningSurveys = learningSurveys;
        _surveyManager = surveyManager;
        _logger = logger;
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

        foreach (var type in LegacyQuizUpgrade.RemovedContentTypes)
        {
            foreach (var item in await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == type).ListAsync())
            {
                _session.Delete(item);
            }

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
        await CreateQuizSurveysAsync();

        return 4;
    }

    private Task DefineQuizPartAsync()
        => _contentDefinitionManager.AlterPartDefinitionAsync(nameof(QuizPart), part => part
            .Attachable(false)
            .WithDisplayName("Quiz")
            .WithDescription("Makes a course item a quiz whose questions, attempts and grading live in a scored survey of the Surveys module the quiz owns."));

    // Every version of a quiz points at the one survey the quiz owns. A quiz whose survey can't be made here gets one
    // when it is next saved.
    private async Task CreateQuizSurveysAsync()
    {
        var versions = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == AssessmentsConstants.ContentTypes.Quiz).ListAsync();

        foreach (var quiz in versions.GroupBy(item => item.ContentItemId, StringComparer.Ordinal))
        {
            var items = quiz.ToList();

            if (!items.Any(LegacyQuizUpgrade.NeedsSurvey))
            {
                continue;
            }

            var latest = items.FirstOrDefault(item => item.Latest) ?? items.FirstOrDefault(item => item.Published) ?? items[^1];

            try
            {
                var survey = await _learningSurveys.CreateOwnedSurveyAsync(latest, SurveyKinds.Quiz, SurveyTemplateNames.Quiz);

                if (survey is null)
                {
                    continue;
                }

                LegacyQuizUpgrade.ApplySettings(LegacyQuizUpgrade.PartOf(latest), survey);
                await _surveyManager.SaveDraftAsync(survey, survey.Draft);

                foreach (var item in items)
                {
                    LegacyQuizUpgrade.SetSurveyId(item, survey.ItemId);
                    await _session.SaveAsync(item);
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogWarning(ex, "The survey of the quiz '{QuizId}' could not be made while upgrading; it is made when the quiz is next saved.", quiz.Key);
            }
        }
    }
}
