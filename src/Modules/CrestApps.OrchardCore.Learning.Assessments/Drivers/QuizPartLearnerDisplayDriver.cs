using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

/// <summary>
/// Shows learners a quiz: their attempts with scores and reviews, and the button that starts or continues one. What an
/// attempt shows follows the quiz's review settings (<see cref="ISurveyReviewPolicy"/>), and whether a new attempt may
/// start, with the attempts left, comes from the access rules Surveys runs on a start, so accommodations and the
/// limit's counting mode apply here as they do there.
/// </summary>
internal sealed class QuizPartLearnerDisplayDriver : ContentPartDisplayDriver<QuizPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISurveyManager _surveyManager;
    private readonly ISurveyVersionCache _versionCache;
    private readonly ICourseAccessService _courseAccess;
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ISurveyReviewPolicy _reviewPolicy;
    private readonly AnonymousCourseSurveyCompletions _anonymousCompletions;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPartLearnerDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="versionCache">The survey version cache.</param>
    /// <param name="courseAccess">The course access service.</param>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="reviewPolicy">The review policy.</param>
    /// <param name="anonymousCompletions">Records anonymous attempts submitted without the learner's request.</param>
    /// <param name="clock">The clock.</param>
    public QuizPartLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ISurveyManager surveyManager,
        ISurveyVersionCache versionCache,
        ICourseAccessService courseAccess,
        ILearningSurveyService learningSurveys,
        ISurveyReviewPolicy reviewPolicy,
        AnonymousCourseSurveyCompletions anonymousCompletions,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _surveyManager = surveyManager;
        _versionCache = versionCache;
        _courseAccess = courseAccess;
        _learningSurveys = learningSurveys;
        _reviewPolicy = reviewPolicy;
        _anonymousCompletions = anonymousCompletions;
        _clock = clock;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(QuizPart part, BuildPartDisplayContext context)
        => Initialize<QuizPanelViewModel>("QuizPart_Learner", async model =>
        {
            var user = _httpContextAccessor.HttpContext?.User;

            model.QuizId = part.ContentItem.ContentItemId;
            model.SignedIn = !string.IsNullOrEmpty(user?.FindFirstValue(ClaimTypes.NameIdentifier));
            model.Survey = string.IsNullOrEmpty(part.SurveyId) ? null : await _surveyManager.FindByIdAsync(part.SurveyId);
            model.Definition = (await _versionCache.GetPublishedAsync(model.Survey))?.Definition;

            if (!model.SignedIn || model.Survey is null)
            {
                return;
            }

            var access = await _courseAccess.GetItemAccessAsync(user, part.ContentItem);

            if (access.Reason != CourseItemAccessReason.Enrolled || access.Enrollment is null)
            {
                return;
            }

            model.Enrolled = true;

            // An anonymous attempt submitted without the learner's request (at its deadline) names nobody; the learner's
            // own visit records it, by the day only, as the course survey panel does.
            if (model.Survey.Access?.Anonymity == AnonymityMode.Anonymous)
            {
                await _anonymousCompletions.CatchUpAsync(model.Survey, access.Enrollment, part.ContentItem.ContentItemId);
            }

            model.Attempts = await _learningSurveys.ListEnrollmentResponsesAsync(model.Survey.ItemId, access.Enrollment.ItemId);
            model.Open = model.Attempts.FirstOrDefault(attempt => attempt.Status == SurveyResponseStatus.InProgress);

            // Each attempt shows what the review settings of the version it ran on allow now.
            var versions = new Dictionary<string, SurveyVersion>(StringComparer.Ordinal);
            var now = _clock.UtcNow;

            // An open attempt past its deadline is submitted when the learner presses the button, and a new attempt starts,
            // so the start form asks for what a new attempt needs.
            model.OpenExpired = model.Open?.DeadlineUtc is { } deadline && now >= deadline;

            foreach (var attempt in model.Attempts)
            {
                var versionId = attempt.SurveyVersionId ?? string.Empty;

                if (!versions.TryGetValue(versionId, out var version))
                {
                    version = string.IsNullOrEmpty(attempt.SurveyVersionId) ? null : await _versionCache.GetAsync(attempt.SurveyVersionId);
                    versions[versionId] = version;
                }

                model.Shows[attempt.ItemId] = _reviewPolicy.GetAllowed(model.Survey, version?.Definition, attempt, now);
            }

            // Whether a new attempt may start, and the attempts left, as the start itself decides them.
            var check = await _learningSurveys.CheckStartAsync(user, part.ContentItem, model.Survey.ItemId);

            model.CanStart = check.CanStart;
            model.AttemptsLeft = check.AttemptsLeft;
            model.NeedsAccessCode = check.NeedsPassword;
            model.Statement = check.Statement;
        }).Location("Detail", "Content:5");
}
