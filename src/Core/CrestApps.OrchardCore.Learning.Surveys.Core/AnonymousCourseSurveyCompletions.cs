using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.Surveys.Core;

/// <summary>
/// Records, on the learner's next visit, an anonymous course survey or quiz that was submitted without the learner's
/// request (at its deadline, or when the survey closed). The response names nobody, so <see cref="LearningSurveyResultObserver"/>
/// can't tell whose progress it completes; the learner's own visit can: the survey's participation record, kept under a
/// pseudonymous key of the learner (the same key the survey's limits count), says they completed a response, and the item
/// is recorded as submitted by the day only, as an anonymous completion always is. Nothing links the learner to the
/// response itself.
/// </summary>
public sealed class AnonymousCourseSurveyCompletions
{
    private readonly SurveyAnonymitySecrets _secrets;
    private readonly SurveyParticipationService _participation;
    private readonly ILearnerProgressService _progress;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymousCourseSurveyCompletions"/> class.
    /// </summary>
    /// <param name="secrets">The anonymity secrets of surveys, for the learner's pseudonymous key.</param>
    /// <param name="participation">The participation records of surveys.</param>
    /// <param name="progress">The learner progress service.</param>
    /// <param name="siteService">The site service, for the site's time zone.</param>
    /// <param name="clock">The clock.</param>
    public AnonymousCourseSurveyCompletions(
        SurveyAnonymitySecrets secrets,
        SurveyParticipationService participation,
        ILearnerProgressService progress,
        ISiteService siteService,
        IClock clock)
    {
        _secrets = secrets;
        _participation = participation;
        _progress = progress;
        _siteService = siteService;
        _clock = clock;
    }

    /// <summary>
    /// Records the item as submitted when the learner completed a response to its anonymous survey that their progress
    /// doesn't show yet.
    /// </summary>
    /// <param name="survey">The survey, as stored (with its secret).</param>
    /// <param name="enrollment">The learner's enrollment in the item's course.</param>
    /// <param name="itemId">The course item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the item was recorded.</returns>
    public async Task<bool> CatchUpAsync(Survey survey, Enrollment enrollment, string itemId, CancellationToken cancellationToken = default)
    {
        if (survey?.Access?.Anonymity != AnonymityMode.Anonymous
            || enrollment is null
            || string.IsNullOrEmpty(enrollment.UserId)
            || string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        if ((await _progress.GetAsync(enrollment.ItemId, cancellationToken))?.Find(itemId)?.State == CourseItemState.Completed)
        {
            return false;
        }

        // The key a start through Learning counts under: the learner, as LearningSurveyService starts them.
        var key = await _secrets.RespondentKeyAsync(survey, new SurveyRespondentIdentity
        {
            UserId = enrollment.UserId,
            Party = new PartyRef(PartyKinds.User, enrollment.UserId),
        }, null, cancellationToken);

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var counts = await _participation.CountAsync(survey, key, LearningSurveyContexts.Course(enrollment.CourseId), _clock.UtcNow);

        if (counts.Completed.Total == 0)
        {
            return false;
        }

        // What the observer records for an anonymous response the learner completed themselves: the completion only, as
        // handed in and graded (a later grade of an anonymous response reaches nobody), so a quiz completes like a survey.
        var site = await _siteService.GetSiteSettingsAsync();
        var result = LearningSurveyResultObserver.CompletionOnly(new CourseItemResult
        {
            ItemId = itemId,
            Submitted = true,
            Attempts = counts.Completed.Total,
        });

        result.AnonymousDayUtc = LearningSurveyResultObserver.StartOfDayUtc(_clock.UtcNow, site?.TimeZoneId);

        var update = await _progress.RecordWithRetryAsync(enrollment, result, cancellationToken);

        return update?.RefusedReason is null;
    }
}
