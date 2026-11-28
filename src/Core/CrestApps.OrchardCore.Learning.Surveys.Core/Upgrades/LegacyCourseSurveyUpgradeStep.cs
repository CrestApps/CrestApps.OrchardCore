using System.Globalization;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Surveys.Core.Upgrades;

/// <summary>
/// Moves the questions of the course surveys and choices of the first Learning release into a survey each item owns, a
/// few items at a time in the background, after the migration has taken the legacy part off the types. An item keeps
/// its legacy part until its survey holds the questions, and points at its survey as soon as the survey exists, so a
/// failure never loses questions and never leaves a second survey made later; an item that still fails is upgraded when
/// it is next saved (<see cref="CourseSurveyPartHandler"/>). The step is safe to run again.
/// </summary>
public sealed class LegacyCourseSurveyUpgradeStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Learning.Surveys.LegacyCourseSurveys";

    private const int ItemsPerRun = 25;
    private const string CursorKey = "after";

    private static readonly string[] _types =
    [
        LearningSurveysConstants.ContentTypes.FeedbackSurvey,
        LearningSurveysConstants.ContentTypes.Choice,
    ];

    private readonly ISession _session;
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ISurveyManager _surveyManager;
    private readonly ISurveyPublisher _publisher;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyCourseSurveyUpgradeStep"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="publisher">The survey publisher.</param>
    /// <param name="logger">The logger.</param>
    public LegacyCourseSurveyUpgradeStep(
        ISession session,
        ILearningSurveyService learningSurveys,
        ISurveyManager surveyManager,
        ISurveyPublisher publisher,
        ILogger<LegacyCourseSurveyUpgradeStep> logger)
    {
        _session = session;
        _learningSurveys = learningSurveys;
        _surveyManager = surveyManager;
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var afterId = context.Data.TryGetValue(CursorKey, out var cursor) && long.TryParse(cursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var rows = (await _session.QueryIndex<ContentItemIndex>(index => index.ContentType.IsIn(_types) && index.Latest && index.Id > afterId)
            .OrderBy(index => index.Id)
            .Take(ItemsPerRun)
            .ListAsync(cancellationToken)).ToList();

        if (rows.Count == 0)
        {
            return true;
        }

        var ids = rows.Select(row => row.ContentItemId).Distinct(StringComparer.Ordinal).ToArray();
        var versions = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId.IsIn(ids)).ListAsync(cancellationToken);

        foreach (var group in versions.GroupBy(item => item.ContentItemId, StringComparer.Ordinal))
        {
            await UpgradeAsync(group.ToList());
        }

        context.Data[CursorKey] = rows[^1].Id.ToString(CultureInfo.InvariantCulture);

        return rows.Count < ItemsPerRun;
    }

    private async Task UpgradeAsync(List<ContentItem> items)
    {
        if (!items.Any(item => LegacyCourseSurveyUpgrade.ReadLegacyPart(item) is not null))
        {
            return;
        }

        var latest = items.FirstOrDefault(item => item.Latest) ?? items.FirstOrDefault(item => item.Published) ?? items[^1];
        var legacy = LegacyCourseSurveyUpgrade.ReadLegacyPart(latest) ?? items.Select(LegacyCourseSurveyUpgrade.ReadLegacyPart).FirstOrDefault(part => part is not null);
        var singleQuestion = string.Equals(latest.ContentType, LearningSurveysConstants.ContentTypes.Choice, StringComparison.Ordinal);

        try
        {
            // A survey made by an earlier run, or by a save in between, is used again: the item never gets a second one.
            var existingId = items.Select(item => (item.TryGet<CourseSurveyPart>(out var part) ? part.SurveyId : null)).FirstOrDefault(id => !string.IsNullOrEmpty(id));
            var survey = string.IsNullOrEmpty(existingId) ? null : await _surveyManager.FindByIdAsync(existingId);

            survey ??= await _learningSurveys.CreateOwnedSurveyAsync(
                latest,
                singleQuestion ? SurveyKinds.Poll : SurveyKinds.Survey,
                singleQuestion ? SurveyTemplateNames.Poll : LearningSurveysConstants.CourseFeedbackTemplate);

            if (survey is null)
            {
                return;
            }

            // Point every version at the survey first, so a failure below leaves no survey nothing points at.
            foreach (var item in items)
            {
                LegacyCourseSurveyUpgrade.SetSurveyId(item, survey.ItemId);
                await _session.SaveAsync(item);
            }

            // A survey course staff built since (by a save in between) keeps its draft: the older questions only go.
            if (LegacySurveyContent.IsNewerThanLegacy(survey))
            {
                foreach (var item in items)
                {
                    LegacyCourseSurveyUpgrade.TakeLegacyPart(item);
                    await _session.SaveAsync(item);
                }

                return;
            }

            var definition = LegacyCourseSurveyUpgrade.ToDefinition(legacy, survey.Title);

            LegacyCourseSurveyUpgrade.ApplySettings(legacy, survey);

            var saved = await _surveyManager.SaveDraftAsync(survey, definition ?? survey.Draft);

            if (saved != SurveyErrorCode.None)
            {
                _logger.LogWarning("The questions of the course item '{ContentItemId}' could not be saved to its survey ({Error}); the item keeps them until it is next saved.", latest.ContentItemId, saved);

                return;
            }

            // The questions are in the survey now; only then does the legacy part go.
            foreach (var item in items)
            {
                LegacyCourseSurveyUpgrade.TakeLegacyPart(item);
                await _session.SaveAsync(item);
            }

            if (definition is not null && items.Any(item => item.Published))
            {
                try
                {
                    await _publisher.PublishAsync(survey.ItemId, "Upgraded from the first Learning release", null);
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    // The questions are kept in the draft, which the course staff publish.
                    _logger.LogWarning(ex, "The upgraded survey of the course item '{ContentItemId}' could not be published; its questions are in the draft.", latest.ContentItemId);
                }
            }
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogWarning(ex, "The survey of the course item '{ContentItemId}' could not be made while upgrading; the item keeps its questions and is upgraded when it is next saved.", latest.ContentItemId);
        }
    }
}
