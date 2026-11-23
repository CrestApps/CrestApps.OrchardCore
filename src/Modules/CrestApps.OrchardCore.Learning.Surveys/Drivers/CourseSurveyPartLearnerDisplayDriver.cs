using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys.Core;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.ViewModels;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Surveys.Drivers;

/// <summary>
/// Shows learners a course survey: whether they answered, and the button that opens the survey.
/// </summary>
internal sealed class CourseSurveyPartLearnerDisplayDriver : ContentPartDisplayDriver<CourseSurveyPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISurveyManager _surveyManager;
    private readonly ISurveyVersionCache _versionCache;
    private readonly ICourseAccessService _courseAccess;
    private readonly ILearningSurveyService _learningSurveys;
    private readonly AnonymousCourseSurveyCompletions _anonymousCompletions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseSurveyPartLearnerDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="versionCache">The survey version cache.</param>
    /// <param name="courseAccess">The course access service.</param>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="anonymousCompletions">Records anonymous responses submitted without the learner's request.</param>
    public CourseSurveyPartLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ISurveyManager surveyManager,
        ISurveyVersionCache versionCache,
        ICourseAccessService courseAccess,
        ILearningSurveyService learningSurveys,
        AnonymousCourseSurveyCompletions anonymousCompletions)
    {
        _httpContextAccessor = httpContextAccessor;
        _surveyManager = surveyManager;
        _versionCache = versionCache;
        _courseAccess = courseAccess;
        _learningSurveys = learningSurveys;
        _anonymousCompletions = anonymousCompletions;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(CourseSurveyPart part, BuildPartDisplayContext context)
        => Initialize<CourseSurveyPanelViewModel>("CourseSurveyPart_Learner", async model =>
        {
            var user = _httpContextAccessor.HttpContext?.User;

            model.ItemId = part.ContentItem.ContentItemId;
            model.Survey = string.IsNullOrEmpty(part.SurveyId) ? null : await _surveyManager.FindByIdAsync(part.SurveyId);
            model.SignedIn = !string.IsNullOrEmpty(user?.FindFirstValue(ClaimTypes.NameIdentifier));
            model.Anonymous = model.Survey?.Access?.Anonymity == AnonymityMode.Anonymous;

            var access = model.Survey is not null && model.SignedIn
                ? await _courseAccess.GetItemAccessAsync(user, part.ContentItem)
                : null;

            if (model.Anonymous && access is { Reason: CourseItemAccessReason.Enrolled, Enrollment: { } enrollment })
            {
                // A response submitted without the learner's request (at its deadline or the survey's close) names
                // nobody; the learner's own visit records it, by the day only, open survey or not.
                await _anonymousCompletions.CatchUpAsync(model.Survey, enrollment, part.ContentItem.ContentItemId);
            }

            if (model.Survey is null || model.Survey.Status != SurveyStatus.Open)
            {
                model.CannotAnswerReason = "NotOpen";

                return;
            }

            if (!model.SignedIn)
            {
                return;
            }

            if (access.Reason != CourseItemAccessReason.Enrolled || access.Enrollment is null)
            {
                model.CannotAnswerReason = access.Reason.ToString();

                return;
            }

            if (!model.Anonymous)
            {
                var responses = await _learningSurveys.ListEnrollmentResponsesAsync(model.Survey.ItemId, access.Enrollment.ItemId);

                model.Response = responses.Count > 0 ? responses[0] : null;
            }

            // Changing a submitted response follows the version it was answered on, as reopening it does; never the
            // unpublished draft.
            var canChange = false;

            if (model.Response is { Status: SurveyResponseStatus.Completed } completed && !string.IsNullOrEmpty(completed.SurveyVersionId))
            {
                var version = await _versionCache.GetAsync(completed.SurveyVersionId);

                canChange = version?.Definition?.Navigation?.AllowEditAfterSubmit == true;
            }

            model.CanAnswer = model.Response is null
                || model.Response.Status == SurveyResponseStatus.InProgress
                || canChange;
        }).Location("Detail", "Content:5");
}
