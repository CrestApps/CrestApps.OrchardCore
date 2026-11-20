using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys.Core.Services;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Learning.Surveys.Controllers;

/// <summary>
/// The learner's side of surveys: answering from the survey panel of the player. Answers are taken for the signed-in
/// learner only, under their own active enrollment, which the response service finds; only the survey comes from the
/// request.
/// </summary>
public sealed class SurveyController : Controller
{
    /// <summary>
    /// The prefix of an answer's form field: <c>q.</c> and the question id.
    /// </summary>
    internal const string AnswerFieldPrefix = "q.";

    private readonly IAuthorizationService _authorizationService;
    private readonly ISurveyResponseService _responses;
    private readonly ISurveyContentSource _contentSource;
    private readonly ICourseStructureService _courseStructure;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyController"/> class.
    /// </summary>
    public SurveyController(
        IAuthorizationService authorizationService,
        ISurveyResponseService responses,
        ISurveyContentSource contentSource,
        ICourseStructureService courseStructure,
        INotifier notifier,
        IHtmlLocalizer<SurveyController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _responses = responses;
        _contentSource = contentSource;
        _courseStructure = courseStructure;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Takes the learner's answers to a survey.
    /// </summary>
    /// <param name="surveyId">The survey item.</param>
    /// <param name="form">The form: one field per question, named <c>q.</c> and the question id.</param>
    /// <returns>Back to the survey in the player.</returns>
    [HttpPost("learning/surveys/{surveyId}/answer", Name = "LearningSurveyAnswer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(string surveyId, IFormCollection form)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return Forbid();
        }

        var survey = await _contentSource.GetPublishedAsync(surveyId);

        if (survey is null || !survey.Has<SurveyPart>())
        {
            return NotFound();
        }

        var answers = new Dictionary<string, IList<string>>(StringComparer.Ordinal);

        foreach (var (key, values) in form ?? new FormCollection([]))
        {
            if (key.StartsWith(AnswerFieldPrefix, StringComparison.Ordinal) && key.Length > AnswerFieldPrefix.Length)
            {
                answers[key[AnswerFieldPrefix.Length..]] = values.Where(value => value is not null).Select(value => value!).ToList();
            }
        }

        var result = await _responses.AnswerAsync(new SurveyAnswerRequest
        {
            UserId = userId,
            SurveyId = survey.ContentItemId,
            Answers = answers,
        }, HttpContext?.RequestAborted ?? default);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Thank you, your answers were saved."]);
        }
        else
        {
            var question = survey.FindSurveyPart()?.Questions.FirstOrDefault(candidate => candidate.Id == result.QuestionId)?.Text;

            await _notifier.WarningAsync(result.Error switch
            {
                SurveyErrors.NotEnrolled => H["You need an active enrollment in the course to answer."],
                SurveyErrors.AccessEnded => H["Your access to this course has ended."],
                SurveyErrors.NotAvailableYet => H["This survey is not open to you yet."],
                SurveyErrors.Closed => H["This survey is closed."],
                SurveyErrors.AlreadyAnswered => H["You already answered this survey."],
                SurveyErrors.Required => H["Answer the question \"{0}\".", question],
                SurveyErrors.InvalidAnswer => H["The answer to \"{0}\" is not one this question takes.", question],
                SurveyErrors.Busy => H["Your answers are being saved. Try again in a moment."],
                _ => H["Your answers could not be saved."],
            });
        }

        var courseId = _courseStructure.GetCourseId(survey);
        var url = string.IsNullOrEmpty(courseId) ? null : Url?.RouteUrl("LearningPlayer", new { courseId, itemId = survey.ContentItemId });

        return LocalRedirect(url ?? "~/");
    }
}
