using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The grading queue, the grading page of a response, score changes and regrades.
/// </summary>
[Admin]
public sealed class SurveyGradingController : Controller
{
    private readonly ISurveyGradingQueue _queue;
    private readonly ISurveyGradingService _grading;
    private readonly ICatalog<Survey> _surveys;
    private readonly ICatalog<SurveyResponse> _responses;
    private readonly ISurveyVersionCache _versionCache;
    private readonly ISurveyControlRegistry _registry;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ISurveyAuthorizationService> _surveyAuthorization;
    private readonly UserManager<IUser> _userManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyGradingController"/> class.
    /// </summary>
    /// <param name="queue">The grading queue.</param>
    /// <param name="grading">The grading service.</param>
    /// <param name="surveys">The survey store.</param>
    /// <param name="responses">The response store.</param>
    /// <param name="versionCache">The version cache.</param>
    /// <param name="registry">The question types.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="surveyAuthorization">The survey authorization service, when registered.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The localizer.</param>
    public SurveyGradingController(
        ISurveyGradingQueue queue,
        ISurveyGradingService grading,
        ICatalog<Survey> surveys,
        ICatalog<SurveyResponse> responses,
        ISurveyVersionCache versionCache,
        ISurveyControlRegistry registry,
        IAuthorizationService authorizationService,
        IEnumerable<ISurveyAuthorizationService> surveyAuthorization,
        UserManager<IUser> userManager,
        INotifier notifier,
        IHtmlLocalizer<SurveyGradingController> htmlLocalizer)
    {
        _queue = queue;
        _grading = grading;
        _surveys = surveys;
        _responses = responses;
        _versionCache = versionCache;
        _registry = registry;
        _authorizationService = authorizationService;
        _surveyAuthorization = surveyAuthorization;
        _userManager = userManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the responses that need a grader.
    /// </summary>
    /// <param name="surveyId">A survey to narrow to, or <see langword="null"/>.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("surveys/grading", "SurveyGradingIndex")]
    public async Task<IActionResult> Index(
        string surveyId,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var allowed = string.IsNullOrEmpty(surveyId)
            ? await _queue.CanOpenAsync(User, HttpContext.RequestAborted)
            : await _queue.CanGradeAsync(User, surveyId, HttpContext.RequestAborted);

        if (!allowed)
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _queue.ListAsync(User, surveyId, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var surveyIds = items.Select(item => item.SurveyId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var titles = surveyIds.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _surveys.GetAsync(surveyIds, HttpContext.RequestAborted)).ToDictionary(survey => survey.ItemId, survey => survey.Title, StringComparer.Ordinal);

        var rows = new List<GradingQueueItem>();

        foreach (var response in items)
        {
            rows.Add(new GradingQueueItem
            {
                Response = response,
                SurveyTitle = titles.GetValueOrDefault(response.SurveyId ?? string.Empty),
                RespondentName = await RespondentNameAsync(response),
                Label = await HttpContext.RequestServices.GetRequiredService<SurveyRespondentNames>().GetLabelAsync(response, HttpContext.RequestAborted),
                IsAnonymous = await HttpContext.RequestServices.GetRequiredService<SurveyRespondentNames>().IsAnonymousAsync(response, HttpContext.RequestAborted),
                PendingQuestions = response.Answers?.Values.Count(answer => answer is not null && answer.ManualScore is null && answer.AutoScore is null && answer.MaxPoints is not null) ?? 0,
            });
        }

        var model = new GradingQueueViewModel
        {
            SurveyId = surveyId,
            SurveyTitle = string.IsNullOrEmpty(surveyId) ? null : titles.GetValueOrDefault(surveyId) ?? (await _surveys.FindByIdAsync(surveyId, HttpContext.RequestAborted))?.Title,
            List = new AdminListContext<GradingQueueItem>
            {
                Source = rows,
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "surveyId", surveyId } }),
            },
        };

        return View(model);
    }

    /// <summary>
    /// Shows the grading page of a response.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("surveys/grading/{responseId}", "SurveyGradingGrade")]
    public async Task<IActionResult> Grade(string responseId)
    {
        var (response, survey, definition) = await LoadAsync(responseId);

        if (response is null)
        {
            return NotFound();
        }

        if (!await _queue.CanGradeAsync(User, response.SurveyId, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (await RefuseAsync(response, survey) is { } refused)
        {
            return refused;
        }

        return View(await BuildAsync(response, survey, definition));
    }

    /// <summary>
    /// Saves the grader's scores and comments.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="model">The posted scores.</param>
    /// <returns>The grading queue, or the page with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Grade))]
    [Admin("surveys/grading/{responseId}", "SurveyGradingGrade")]
    public async Task<IActionResult> GradePost(string responseId, GradeResponseViewModel model)
    {
        var (response, survey, definition) = await LoadAsync(responseId);

        if (response is null)
        {
            return NotFound();
        }

        if (!await _queue.CanGradeAsync(User, response.SurveyId, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (await RefuseAsync(response, survey) is { } refused)
        {
            return refused;
        }

        if (SurveyScoreChangeChecks.GradingChangesGivenScores(response) && !await CanChangeScoresAsync(survey, response))
        {
            return Forbid();
        }

        var scores = (model.Points ?? [])
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);

        var result = await _grading.GradeAsync(responseId, scores, model.Comments, model.Feedback, _userManager.GetUserId(User), HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await NotifyErrorAsync(result.Error);

            return View(await BuildAsync(response, survey, definition));
        }

        foreach (var questionId in model.AIAssisted ?? [])
        {
            await _grading.RecordScoringHelpAsync(responseId, questionId, HttpContext.RequestAborted);
        }

        await _notifier.SuccessAsync(H["The response has been graded."]);

        return RedirectToAction(nameof(Index), new { surveyId = response.SurveyId });
    }

    /// <summary>
    /// Sets the total score of a response.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="model">The new score and the reason.</param>
    /// <returns>The grading page.</returns>
    [HttpPost]
    [Admin("surveys/grading/{responseId}/score", "SurveyGradingChangeScore")]
    public async Task<IActionResult> ChangeScore(string responseId, GradeResponseViewModel model)
    {
        var response = await _responses.FindByIdAsync(responseId, HttpContext.RequestAborted);

        if (response is null)
        {
            return NotFound();
        }

        if (!await _queue.CanGradeAsync(User, response.SurveyId, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var survey = await _surveys.FindByIdAsync(response.SurveyId, HttpContext.RequestAborted);

        if (await RefuseAsync(response, survey) is { } refused)
        {
            return refused;
        }

        if (!await CanChangeScoresAsync(survey, response))
        {
            return Forbid();
        }

        var result = model.NewScore is { } score
            ? await _grading.ChangeScoreAsync(responseId, score, _userManager.GetUserId(User), model.Reason, HttpContext.RequestAborted)
            : new SurveyResponseResult { Error = SurveyErrorCode.InvalidAnswer };

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The score has been changed."]);
        }
        else
        {
            await NotifyErrorAsync(result.Error);
        }

        return RedirectToAction(nameof(Grade), new { responseId });
    }

    /// <summary>
    /// Scores a response again with its version's key.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="model">The reason.</param>
    /// <returns>The grading page.</returns>
    [HttpPost]
    [Admin("surveys/grading/{responseId}/regrade", "SurveyGradingRegrade")]
    public async Task<IActionResult> Regrade(string responseId, GradeResponseViewModel model)
    {
        var response = await _responses.FindByIdAsync(responseId, HttpContext.RequestAborted);

        if (response is null)
        {
            return NotFound();
        }

        if (!await _queue.CanGradeAsync(User, response.SurveyId, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var survey = await _surveys.FindByIdAsync(response.SurveyId, HttpContext.RequestAborted);

        if (await RefuseAsync(response, survey) is { } refused)
        {
            return refused;
        }

        if (!await CanChangeScoresAsync(survey, response))
        {
            return Forbid();
        }

        var result = await _grading.RegradeAsync(responseId, _userManager.GetUserId(User), model.Reason, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The response has been scored again."]);
        }
        else
        {
            await NotifyErrorAsync(result.Error);
        }

        return RedirectToAction(nameof(Grade), new { responseId });
    }

    /// <summary>
    /// Shows the page that scores older versions again with another version's key.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("surveys/{surveyId}/regrade", "SurveyGradingRegradeVersions")]
    public async Task<IActionResult> RegradeVersions(string surveyId)
    {
        var survey = await _surveys.FindByIdAsync(surveyId, HttpContext.RequestAborted);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await CanGradeSurveyAsync(survey) || !await CanChangeScoresAsync(survey, null))
        {
            return Forbid();
        }

        return View(await BuildRegradeAsync(survey, new RegradeVersionsViewModel()));
    }

    /// <summary>
    /// Scores the responses of the chosen versions again.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="model">The versions, the key version and the reason.</param>
    /// <returns>The survey's grading queue, or the page with errors.</returns>
    [HttpPost]
    [ActionName(nameof(RegradeVersions))]
    [Admin("surveys/{surveyId}/regrade", "SurveyGradingRegradeVersions")]
    public async Task<IActionResult> RegradeVersionsPost(string surveyId, RegradeVersionsViewModel model)
    {
        var survey = await _surveys.FindByIdAsync(surveyId, HttpContext.RequestAborted);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await CanGradeSurveyAsync(survey) || !await CanChangeScoresAsync(survey, null))
        {
            return Forbid();
        }

        if (model.FromVersionIds is not { Count: > 0 })
        {
            ModelState.AddModelError(nameof(model.FromVersionIds), H["Pick the versions whose responses are scored again."].Value);
        }

        if (string.IsNullOrEmpty(model.KeyVersionId))
        {
            ModelState.AddModelError(nameof(model.KeyVersionId), H["Pick the version whose key is used."].Value);
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), H["Say why the responses are scored again."].Value);
        }

        if (ModelState.IsValid && await HasOwnResponseAsync(survey, model.FromVersionIds))
        {
            // Nobody regrades their own work, so a grader who answered one of these versions leaves the regrade to someone else.
            ModelState.AddModelError(nameof(model.FromVersionIds), H["You have a response in one of these versions. Someone else must score them again."].Value);
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildRegradeAsync(survey, model));
        }

        var result = await _grading.RegradeVersionsAsync(surveyId, model.FromVersionIds, model.KeyVersionId, _userManager.GetUserId(User), model.Reason, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["{0} responses have been scored again.", result.Count]);

        if (result.SkippedQuestions.Count > 0)
        {
            var skipped = string.Join(", ", result.SkippedQuestions.Select(question => H["{0} (version {1})", question.Title, question.VersionNumber].Value));

            await _notifier.WarningAsync(H["These questions kept their scores because the key names choices their version does not have: {0}. Grade them by hand if they need a change.", skipped]);
        }

        return RedirectToAction(nameof(Index), new { surveyId });
    }

    private async Task<RegradeVersionsViewModel> BuildRegradeAsync(Survey survey, RegradeVersionsViewModel model)
    {
        model.SurveyId = survey.ItemId;
        model.SurveyTitle = survey.Title;
        // The version index gives the ids and numbers; no version is loaded to fill the list.
        model.Versions = (await HttpContext.RequestServices.GetRequiredService<SurveyVersionStore>().ListRowsAsync(survey.ItemId, HttpContext.RequestAborted))
            .Select(version => new SelectListItem(H["Version {0}", version.Number].Value, version.ItemId))
            .ToList();
        model.KeyVersionId ??= survey.PublishedVersionId;

        return model;
    }

    private async Task<GradeResponseViewModel> BuildAsync(SurveyResponse response, Survey survey, SurveyDefinition definition)
    {
        var model = new GradeResponseViewModel
        {
            Response = response,
            SurveyTitle = survey?.Title,
            CanChangeScores = await CanChangeScoresAsync(survey, response),
            RespondentName = await RespondentNameAsync(response),
            Label = SurveyAnonymity.ResponseLabel(survey, response),
            IsAnonymous = survey is null || SurveyAnonymity.IsAnonymous(survey),
            Feedback = response.Feedback,
        };

        foreach (var question in definition?.Pages.SelectMany(page => page.Questions) ?? [])
        {
            if (question is null || _registry.GetScorer(question.ControlType) is null)
            {
                continue;
            }

            response.Answers.TryGetValue(question.Id, out var answer);

            if (answer?.MaxPoints is null)
            {
                // A question the respondent was not shown, or one that gives no points.
                continue;
            }

            var handler = _registry.GetHandler(question.ControlType);

            model.Questions.Add(new GradeQuestionViewModel
            {
                QuestionId = question.Id,
                Title = question.Title,
                Answer = handler?.FormatAnswer(question, answer, new() { Definition = definition }),
                Points = answer.Score,
                MaxPoints = answer.MaxPoints.Value,
                NeedsGrading = answer.Score is null,
                Comment = answer.GraderComment,
                GraderGuidance = question.Scoring?.GraderGuidance,
                AIAssisted = answer.AIAssisted,
            });
        }

        return model;
    }

    private async Task<(SurveyResponse Response, Survey Survey, SurveyDefinition Definition)> LoadAsync(string responseId)
    {
        var response = string.IsNullOrEmpty(responseId) ? null : await _responses.FindByIdAsync(responseId, HttpContext.RequestAborted);

        if (response is null)
        {
            return (null, null, null);
        }

        response.Answers ??= [];

        var survey = await _surveys.FindByIdAsync(response.SurveyId, HttpContext.RequestAborted);
        var version = await _versionCache.GetAsync(response.SurveyVersionId, HttpContext.RequestAborted);

        return (response, survey, version?.Definition ?? survey?.Draft);
    }

    // Every grading path of one response: nobody grades, rescores or regrades their own work, and a real response of an
    // anonymous survey opens under the shared small-group rule (as Details does), so a grader can't read the first
    // answers one at a time before there is a crowd to hide in.
    private async Task<IActionResult> RefuseAsync(SurveyResponse response, Survey survey)
    {
        if (SurveyOwnWork.IsOwn(User, response))
        {
            await _notifier.ErrorAsync(H["You can't grade your own response. Someone else must grade it."]);

            return RedirectToAction(nameof(Index), new { surveyId = response.SurveyId });
        }

        if (survey is null || !SurveyAnonymity.IsAnonymous(survey) || response.IsTest)
        {
            return null;
        }

        var decision = await HttpContext.RequestServices.GetRequiredService<SurveyAnonymousGroups>().DecideAsync(survey, null, [response.Status], HttpContext.RequestAborted);

        if (decision.Shows(response.Status, response.IsTest))
        {
            return null;
        }

        await _notifier.WarningAsync(H["This survey is anonymous, and it has fewer than {0} completed responses. Its responses can be graded once there are enough, so no one can be recognized.", decision.MinimumGroupSize]);

        return RedirectToAction(nameof(Index), new { surveyId = response.SurveyId });
    }

    private async Task<bool> HasOwnResponseAsync(Survey survey, IReadOnlyCollection<string> versionIds)
    {
        var userId = SurveyOwnWork.UserIdOf(User);

        if (string.IsNullOrEmpty(userId) || versionIds is not { Count: > 0 })
        {
            return false;
        }

        var queries = HttpContext.RequestServices.GetRequiredService<ISurveyResponseQueries>();

        foreach (var version in await HttpContext.RequestServices.GetRequiredService<SurveyVersionStore>().ListRowsAsync(survey.ItemId, HttpContext.RequestAborted))
        {
            if (!versionIds.Contains(version.ItemId, StringComparer.Ordinal))
            {
                continue;
            }

            var (count, _) = await queries.ListAsync(new SurveyResponseQuery
            {
                SurveyIds = [survey.ItemId],
                UserId = userId,
                VersionNumber = version.Number,
                Take = 1,
            }, HttpContext.RequestAborted);

            if (count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> CanGradeSurveyAsync(Survey survey)
    {
        if (await _authorizationService.AuthorizeAsync(User, SurveysPermissions.GradeSurveyResponses))
        {
            return true;
        }

        return _surveyAuthorization.FirstOrDefault() is { } authorization
            && await authorization.AuthorizeAsync(User, survey, SurveyOperation.Grade, HttpContext.RequestAborted);
    }

    // Changing given scores (the total, a regrade, grades of a graded response, another version's key) passes every
    // registered score change check on top of the grading permission, such as Override grades on a course quiz.
    private Task<bool> CanChangeScoresAsync(Survey survey, SurveyResponse response)
        => HttpContext.RequestServices.GetRequiredService<SurveyScoreChangeChecks>().AllowAsync(User, survey, response, HttpContext.RequestAborted);

    // The shared respondent names: a grader sees who responded on an identified survey only (Guest or Deleted user
    // when there is no user), and Anonymous on an anonymous survey.
    private async Task<string> RespondentNameAsync(SurveyResponse response)
        => response is null
            ? null
            : await HttpContext.RequestServices.GetRequiredService<SurveyRespondentNames>().GetAsync(response, HttpContext.RequestAborted);

    private ValueTask NotifyErrorAsync(SurveyErrorCode error)
        => error switch
        {
            SurveyErrorCode.NotFound => _notifier.ErrorAsync(H["The response could not be found."]),
            SurveyErrorCode.InvalidState => _notifier.ErrorAsync(H["Only a completed response of a scored survey can be graded."]),
            SurveyErrorCode.Busy => _notifier.ErrorAsync(H["Someone changed the response meanwhile. Reload the page and try again."]),
            _ => _notifier.ErrorAsync(H["Check the points: each must be between 0 and the most points of its question, and a score change needs a reason."]),
        };
}
