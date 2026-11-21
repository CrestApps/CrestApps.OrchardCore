using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

/// <summary>
/// Shows learners a quiz: their attempts with scores and reviews, and the button that starts or continues one.
/// </summary>
internal sealed class QuizPartLearnerDisplayDriver : ContentPartDisplayDriver<QuizPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISurveyManager _surveyManager;
    private readonly ISurveyVersionCache _versionCache;
    private readonly ICourseAccessService _courseAccess;
    private readonly ILearningSurveyService _learningSurveys;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPartLearnerDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="versionCache">The survey version cache.</param>
    /// <param name="courseAccess">The course access service.</param>
    /// <param name="learningSurveys">The Learning survey service.</param>
    public QuizPartLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ISurveyManager surveyManager,
        ISurveyVersionCache versionCache,
        ICourseAccessService courseAccess,
        ILearningSurveyService learningSurveys)
    {
        _httpContextAccessor = httpContextAccessor;
        _surveyManager = surveyManager;
        _versionCache = versionCache;
        _courseAccess = courseAccess;
        _learningSurveys = learningSurveys;
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
            model.Attempts = await _learningSurveys.ListEnrollmentResponsesAsync(model.Survey.ItemId, access.Enrollment.ItemId);
            model.Open = model.Attempts.FirstOrDefault(attempt => attempt.Status == SurveyResponseStatus.InProgress);

            var counted = model.Attempts.Count(attempt => attempt.Status != SurveyResponseStatus.Voided);

            model.AttemptsLeft = model.Survey.Access?.Limits?.Total is { } total ? Math.Max(total - counted, 0) : null;
        }).Location("Detail", "Content:5");
}
