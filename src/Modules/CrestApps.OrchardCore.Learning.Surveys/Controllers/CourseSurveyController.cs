using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Learning.Surveys.Controllers;

/// <summary>
/// Opens the survey of a course survey or choice for the signed-in learner, through their enrollment.
/// </summary>
[Authorize]
public sealed class CourseSurveyController : Controller
{
    private readonly IContentManager _contentManager;
    private readonly ILearningSurveyService _learningSurveys;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseSurveyController"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public CourseSurveyController(
        IContentManager contentManager,
        ILearningSurveyService learningSurveys,
        INotifier notifier,
        IHtmlLocalizer<CourseSurveyController> htmlLocalizer)
    {
        _contentManager = contentManager;
        _learningSurveys = learningSurveys;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Starts or resumes the learner's response and opens it.
    /// </summary>
    /// <param name="itemId">The course item.</param>
    /// <param name="returnUrl">The page of the course item to come back to.</param>
    /// <returns>A redirect to the survey, or back with a message.</returns>
    [HttpPost("learning/surveys/{itemId}/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string itemId, string returnUrl)
    {
        var item = string.IsNullOrEmpty(itemId) ? null : await _contentManager.GetAsync(itemId, VersionOptions.Published);
        var part = item is not null && item.TryGet<CourseSurveyPart>(out var found) ? found : null;

        if (part is null || string.IsNullOrEmpty(part.SurveyId))
        {
            return NotFound();
        }

        var back = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
        var result = await _learningSurveys.StartAsync(User, item, part.SurveyId, back, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(result.Error == SurveyErrorCode.LimitReached
                ? H["You already answered this survey."]
                : H["You cannot answer this survey now."]);

            return LocalRedirect(back);
        }

        var url = Url.Content("~/s/r/" + result.Response.ItemId);

        return LocalRedirect(string.IsNullOrEmpty(result.ResumeToken) ? url : url + "?t=" + Uri.EscapeDataString(result.ResumeToken));
    }
}
