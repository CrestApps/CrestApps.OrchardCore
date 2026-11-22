using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Surveys.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Surveys.Models;
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
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;
using OrchardCore.Title.Models;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Surveys.Migrations;

/// <summary>
/// Defines the course survey part and the feedback survey and choice course item types, and seeds the course
/// feedback survey template. On a site from the first Learning release (whose migration was <c>SurveysMigrations</c>,
/// with the questions in a <c>SurveyPart</c> of each item), it also upgrades the types and items: see
/// <see cref="CreateAsync"/>.
/// </summary>
internal sealed class LearningSurveysMigrations : DataMigration
{
    // The first release kept its responses in a collection of its own.
    private static readonly string[] _types =
    [
        LearningSurveysConstants.ContentTypes.FeedbackSurvey,
        LearningSurveysConstants.ContentTypes.Choice,
    ];

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;
    private readonly IRecipeMigrator _recipeMigrator;
    private readonly ISession _session;
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ISurveyManager _surveyManager;
    private readonly ISurveyPublisher _publisher;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveysMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="courseListSynchronizer">The course list synchronizer.</param>
    /// <param name="recipeMigrator">The recipe migrator.</param>
    /// <param name="session">The session.</param>
    /// <param name="learningSurveys">The Learning survey service, which creates the surveys of upgraded items.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="publisher">The survey publisher.</param>
    /// <param name="logger">The logger.</param>
    public LearningSurveysMigrations(
        IContentDefinitionManager contentDefinitionManager,
        CourseListSynchronizer courseListSynchronizer,
        IRecipeMigrator recipeMigrator,
        ISession session,
        ILearningSurveyService learningSurveys,
        ISurveyManager surveyManager,
        ISurveyPublisher publisher,
        ILogger<LearningSurveysMigrations> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
        _recipeMigrator = recipeMigrator;
        _session = session;
        _learningSurveys = learningSurveys;
        _surveyManager = surveyManager;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Defines the part and the types. It runs on a fresh site, and once on a site from the first Learning release, whose
    /// migration had another class name: there the types still hold the old <c>SurveyPart</c>, which is now the name of
    /// the Surveys embed part. The step then removes it from the types, moves each item's questions into a survey the
    /// item owns, and drops the old responses table. Every other step defines the same result again, so it changes
    /// nothing that is already there.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        var upgrading = await HasLegacyPartAsync();

        if (upgrading)
        {
            foreach (var type in _types)
            {
                await _contentDefinitionManager.AlterTypeDefinitionAsync(type, builder => builder.RemovePart(LegacyCourseSurveyUpgrade.LegacyPartName));
            }
        }

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CourseSurveyPart), part => part
            .Attachable(false)
            .WithDisplayName("Course survey")
            .WithDescription("Makes a course item a survey whose questions live in a survey of the Surveys module the item owns."));

        await DefineAsync(LearningSurveysConstants.ContentTypes.FeedbackSurvey, "Feedback survey", "Questions learners answer about the course, with the totals for the course staff.", singleQuestion: false);
        await DefineAsync(LearningSurveysConstants.ContentTypes.Choice, "Choice", "One question learners answer by picking a choice, as a poll, with totals learners may see.", singleQuestion: true);

        await _courseListSynchronizer.SynchronizeAsync();

        // The course feedback template belongs to Learning: feedback survey items start from it, and administrators
        // may change it on the Survey templates page.
        await _recipeMigrator.ExecuteAsync($"course-feedback-template{RecipesConstants.RecipeExtension}", this);

        if (upgrading)
        {
            // The first release kept responses in its own table; they are not carried over.
            await SchemaBuilder.DropTableAsync("LearningSurveyResponses_SurveyResponseIndex");
            await UpgradeItemsAsync();
        }

        return 1;
    }

    private async Task<bool> HasLegacyPartAsync()
    {
        foreach (var type in _types)
        {
            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(type);

            if (definition?.Parts.Any(part => string.Equals(part.Name, LegacyCourseSurveyUpgrade.LegacyPartName, StringComparison.Ordinal)) == true)
            {
                return true;
            }
        }

        return false;
    }

    // Each item gets a survey built from the questions its old part held, published when the item is, and every version
    // of the item points at it. An item whose survey can't be made here gets one when it is next saved.
    private async Task UpgradeItemsAsync()
    {
        foreach (var type in _types)
        {
            var singleQuestion = string.Equals(type, LearningSurveysConstants.ContentTypes.Choice, StringComparison.Ordinal);
            var versions = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == type).ListAsync();

            foreach (var group in versions.GroupBy(item => item.ContentItemId, StringComparer.Ordinal))
            {
                var items = group.ToList();
                var latest = items.FirstOrDefault(item => item.Latest) ?? items.FirstOrDefault(item => item.Published) ?? items[^1];
                var legacy = LegacyCourseSurveyUpgrade.TakeLegacyPart(latest);

                foreach (var item in items.Where(item => !ReferenceEquals(item, latest)))
                {
                    LegacyCourseSurveyUpgrade.TakeLegacyPart(item);
                }

                try
                {
                    var survey = await _learningSurveys.CreateOwnedSurveyAsync(
                        latest,
                        singleQuestion ? SurveyKinds.Poll : SurveyKinds.Survey,
                        singleQuestion ? SurveyTemplateNames.Poll : LearningSurveysConstants.CourseFeedbackTemplate);

                    if (survey is not null)
                    {
                        var definition = LegacyCourseSurveyUpgrade.ToDefinition(legacy, survey.Title);

                        LegacyCourseSurveyUpgrade.ApplySettings(legacy, survey);
                        await _surveyManager.SaveDraftAsync(survey, definition ?? survey.Draft);

                        if (definition is not null && items.Any(item => item.Published))
                        {
                            await _publisher.PublishAsync(survey.ItemId, "Upgraded from the first Learning release", null);
                        }

                        foreach (var item in items)
                        {
                            LegacyCourseSurveyUpgrade.SetSurveyId(item, survey.ItemId);
                        }
                    }
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    _logger.LogWarning(ex, "The survey of the course item '{ContentItemId}' could not be made while upgrading; it is made when the item is next saved.", group.Key);
                }

                foreach (var item in items)
                {
                    await _session.SaveAsync(item);
                }
            }
        }
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
