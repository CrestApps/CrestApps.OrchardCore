using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// Starts or continues the signed-in learner's attempt at a quiz, through their enrollment, and opens it in the Surveys
/// runner.
/// </summary>
[Authorize]
public sealed class QuizAttemptController : Controller
{
    private readonly IContentManager _contentManager;
    private readonly ILearningSurveyService _learningSurveys;
    private readonly INotifier _notifier;
    private readonly SurveyResumeCookies _resumeCookies;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizAttemptController"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="resumeCookies">The survey resume cookies.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public QuizAttemptController(
        IContentManager contentManager,
        ILearningSurveyService learningSurveys,
        INotifier notifier,
        SurveyResumeCookies resumeCookies,
        IHtmlLocalizer<QuizAttemptController> htmlLocalizer)
    {
        _contentManager = contentManager;
        _learningSurveys = learningSurveys;
        _notifier = notifier;
        _resumeCookies = resumeCookies;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Starts or continues an attempt and opens it.
    /// </summary>
    /// <param name="quizId">The quiz course item.</param>
    /// <param name="returnUrl">The page of the quiz to come back to.</param>
    /// <returns>A redirect to the attempt, or back with a message.</returns>
    [HttpPost("learning/quizzes/{quizId}/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string quizId, string returnUrl)
    {
        var item = string.IsNullOrEmpty(quizId) ? null : await _contentManager.GetAsync(quizId, VersionOptions.Published);
        var part = item is not null && item.TryGet<QuizPart>(out var found) ? found : null;

        if (part is null || string.IsNullOrEmpty(part.SurveyId))
        {
            return NotFound();
        }

        var back = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
        var result = await _learningSurveys.StartAsync(User, item, part.SurveyId, back, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(result.Error switch
            {
                SurveyErrorCode.LimitReached => H["You have no attempts left."],
                SurveyErrorCode.CooldownActive => H["You must wait before your next attempt."],
                SurveyErrorCode.Closed or SurveyErrorCode.NotOpen or SurveyErrorCode.NotAvailableNow => H["This quiz is not open now."],
                _ => H["You cannot attempt this quiz now."],
            });

            return LocalRedirect(back);
        }

        // The resume token goes in the survey's cookie, never in a URL.
        if (!string.IsNullOrEmpty(result.ResumeToken))
        {
            _resumeCookies.Write(result.Response.SurveyId, result.ResumeToken);
        }

        return LocalRedirect(Url.Content("~/s/r/" + Uri.EscapeDataString(result.Response.ItemId)));
    }
}
