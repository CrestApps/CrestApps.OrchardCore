using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Upgrades;

/// <summary>
/// Finishes the upgrade of a site from the first Learning release in the background, a batch at a time: it deletes the
/// content items of the removed question types (the first-release questions and question bank items, whose questions
/// are not carried over; the log records how many), then gives every quiz a scored survey with its first-release
/// settings.
/// A quiz points at its survey as soon as the survey exists, so a failure never leaves a survey nothing points at (and a
/// second one made when the quiz is next saved). The step is safe to run again.
/// </summary>
public sealed class LegacyQuizUpgradeStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Learning.Assessments.LegacyQuizzes";

    private const int ItemsPerRun = 50;
    private const string PhaseKey = "phase";
    private const string CursorKey = "after";
    private const string QuizzesPhase = "quizzes";
    private const string RemovedKey = "removed";

    private readonly ISession _session;
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ISurveyManager _surveyManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyQuizUpgradeStep"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="logger">The logger.</param>
    public LegacyQuizUpgradeStep(
        ISession session,
        ILearningSurveyService learningSurveys,
        ISurveyManager surveyManager,
        ILogger<LegacyQuizUpgradeStep> logger)
    {
        _session = session;
        _learningSurveys = learningSurveys;
        _surveyManager = surveyManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Data.TryGetValue(PhaseKey, out var phase) || !string.Equals(phase, QuizzesPhase, StringComparison.Ordinal))
        {
            // The removed question types: their items are deleted a batch at a time, each run reading from the start
            // because the deleted rows are gone.
            var removedTypes = LegacyQuizUpgrade.RemovedContentTypes.ToArray();
            var removed = (await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(removedTypes))
                .OrderBy(index => index.Id)
                .Take(ItemsPerRun)
                .ListAsync(cancellationToken)).ToList();

            foreach (var item in removed)
            {
                _session.Delete(item);
            }

            var total = (context.Data.TryGetValue(RemovedKey, out var counted) && int.TryParse(counted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var before) ? before : 0) + removed.Count;

            context.Data[RemovedKey] = total.ToString(CultureInfo.InvariantCulture);

            if (removed.Count > 0)
            {
                _logger.LogWarning(
                    "The Learning upgrade deleted {Count} first-release question and question bank items ({Total} so far); their questions are not carried over to the quizzes' surveys.",
                    removed.Count,
                    total);
            }

            if (removed.Count == ItemsPerRun)
            {
                return false;
            }

            context.Data[PhaseKey] = QuizzesPhase;
            context.Data[CursorKey] = "0";
        }

        var afterId = context.Data.TryGetValue(CursorKey, out var cursor) && long.TryParse(cursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var rows = (await _session.QueryIndex<ContentItemIndex>(index => index.ContentType == AssessmentsConstants.ContentTypes.Quiz && index.Latest && index.Id > afterId)
            .OrderBy(index => index.Id)
            .Take(ItemsPerRun)
            .ListAsync(cancellationToken)).ToList();

        if (rows.Count == 0)
        {
            return true;
        }

        var ids = rows.Select(row => row.ContentItemId).Distinct(StringComparer.Ordinal).ToArray();
        var versions = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId.IsIn(ids)).ListAsync(cancellationToken);

        foreach (var quiz in versions.GroupBy(item => item.ContentItemId, StringComparer.Ordinal))
        {
            await UpgradeAsync(quiz.ToList());
        }

        context.Data[CursorKey] = rows[^1].Id.ToString(CultureInfo.InvariantCulture);

        return rows.Count < ItemsPerRun;
    }

    private async Task UpgradeAsync(List<ContentItem> items)
    {
        if (!items.Any(item => LegacyQuizUpgrade.NeedsSurvey(item) || LegacyQuizUpgrade.HoldsLegacySettings(item)))
        {
            return;
        }

        var latest = items.FirstOrDefault(item => item.Latest) ?? items.FirstOrDefault(item => item.Published) ?? items[^1];

        try
        {
            // A survey a version already points at is used again, so the quiz never gets a second one.
            var existingId = items
                .Select(item => (item.TryGet<QuizPart>(out var part) ? part.SurveyId : null))
                .FirstOrDefault(id => !string.IsNullOrEmpty(id));
            var survey = string.IsNullOrEmpty(existingId) ? null : await _surveyManager.FindByIdAsync(existingId);

            survey ??= await _learningSurveys.CreateOwnedSurveyAsync(latest, SurveyKinds.Quiz, SurveyTemplateNames.Quiz);

            if (survey is null)
            {
                return;
            }

            // The quiz points at its survey before anything else can fail, so it never gets a second one; its
            // first-release settings stay on the item until the survey has saved them.
            var legacy = LegacyQuizUpgrade.PartOf(latest)?.DeepClone() as JsonObject;

            foreach (var item in items)
            {
                LegacyQuizUpgrade.PointAt(item, survey.ItemId);
                await _session.SaveAsync(item);
            }

            // A survey course staff built since (by a save in between) keeps its settings; otherwise it takes the pass
            // mark, time limit, attempts and window, and only a saved draft lets the item drop them.
            if (items.Any(LegacyQuizUpgrade.HoldsLegacySettings) && !LegacySurveyContent.IsNewerThanLegacy(survey))
            {
                LegacyQuizUpgrade.ApplySettings(legacy, survey);

                var saved = await _surveyManager.SaveDraftAsync(survey, survey.Draft);

                if (saved != SurveyErrorCode.None)
                {
                    _logger.LogWarning("The settings of the quiz '{QuizId}' could not be saved to its survey ({Error}); the quiz item keeps them, so nothing is lost.", latest.ContentItemId, saved);

                    return;
                }
            }

            foreach (var item in items)
            {
                LegacyQuizUpgrade.SetSurveyId(item, survey.ItemId);
                await _session.SaveAsync(item);
            }
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogWarning(ex, "The survey of the quiz '{QuizId}' could not be made while upgrading; it is made when the quiz is next saved.", latest.ContentItemId);
        }
    }
}
