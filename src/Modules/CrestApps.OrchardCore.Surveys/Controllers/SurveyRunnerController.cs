using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.ReCaptcha.Services;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The respondent pages: the landing page of a link, the pages of a response, autosave, live evaluation, immediate
/// feedback, the ending, the review, the totals of a poll and the respondent's own responses. The controller is thin:
/// the response service decides everything.
/// </summary>
public sealed class SurveyRunnerController : Controller
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions _ruleOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private const string CultureQueryKey = "culture";

    private static readonly HashSet<string> _reservedQueryKeys = new(StringComparer.OrdinalIgnoreCase) { SurveyRespondentIdentityFactory.ResumeQueryKey, "preview", "hidden", CultureQueryKey };

    private readonly ISurveyResponseService _responses;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyResponseStore _responseStore;
    private readonly SurveyRuntimeVersions _versions;
    private readonly ISurveyVersionCache _versionCache;
    private readonly SurveyRespondentIdentityFactory _identities;
    private readonly SurveyRunnerShapes _shapes;
    private readonly SurveyEndingService _endings;
    private readonly SurveyResponseCounter _counter;
    private readonly SurveyErrorMessages _messages;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRunnerController"/> class.
    /// </summary>
    /// <param name="responses">The response service.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="responseStore">The response store.</param>
    /// <param name="versions">The runtime versions.</param>
    /// <param name="versionCache">The version cache.</param>
    /// <param name="identities">The respondent identity factory.</param>
    /// <param name="shapes">The runner shapes.</param>
    /// <param name="endings">The ending service.</param>
    /// <param name="counter">The response counter.</param>
    /// <param name="messages">The error messages.</param>
    /// <param name="serviceProvider">The services, for optional CAPTCHA and results aggregates.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyRunnerController(
        ISurveyResponseService responses,
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        SurveyResponseStore responseStore,
        SurveyRuntimeVersions versions,
        ISurveyVersionCache versionCache,
        SurveyRespondentIdentityFactory identities,
        SurveyRunnerShapes shapes,
        SurveyEndingService endings,
        SurveyResponseCounter counter,
        SurveyErrorMessages messages,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<SurveyRunnerController> stringLocalizer)
    {
        _responses = responses;
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _responseStore = responseStore;
        _versions = versions;
        _versionCache = versionCache;
        _identities = identities;
        _shapes = shapes;
        _endings = endings;
        _counter = counter;
        _messages = messages;
        _serviceProvider = serviceProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the landing page of a link. When the survey skips it, the page posts its start form at once (a GET never
    /// starts a response, so link previews and mail scanners create nothing); without script it shows a Start button.
    /// </summary>
    /// <param name="token">The collector token.</param>
    /// <returns>The page.</returns>
    [HttpGet("s/{token}")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Landing(string token)
    {
        var (survey, collector) = await FindByTokenAsync(token);

        if (survey is null)
        {
            return NotFoundPage();
        }

        var model = await LandingModelAsync(token, survey, collector);

        if (collector.Kind == SurveyCollectorKinds.Kiosk)
        {
            _identities.ClearResumeCookie(survey.ItemId);
        }
        else if (model.CanStart && !model.NeedsPassword && !model.NeedsCaptcha && model.SkipLanding)
        {
            model.AutoStart = true;
        }

        return View("Landing", model);
    }

    /// <summary>
    /// Starts a response from a link.
    /// </summary>
    /// <param name="token">The collector token.</param>
    /// <param name="password">The collector password entered, or <see langword="null"/>.</param>
    /// <returns>A redirect to the first page, or the landing page with the reason.</returns>
    [HttpPost("s/{token}/start")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Start(string token, string password)
    {
        var (survey, collector) = await FindByTokenAsync(token);

        if (survey is null)
        {
            return NotFoundPage();
        }

        var captchaPassed = false;

        if (survey.Access?.RequireCaptcha == true && _serviceProvider.GetService<ReCaptchaService>() is { } captcha)
        {
            captchaPassed = await captcha.ValidateCaptchaAsync((_, _) => { });
        }

        return await StartCoreAsync(token, survey, collector, password, captchaPassed);
    }

    /// <summary>
    /// Shows the current page of a response.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="preview">Whether this is a staff preview.</param>
    /// <param name="hidden">Whether a preview shows hidden questions.</param>
    /// <returns>The page.</returns>
    [HttpGet("s/r/{responseId}")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Respond(string responseId, bool preview = false, bool hidden = false)
    {
        // A resume link carries its token once: it is traded for a new token in the cookie, and the page redirects to
        // the address without it, so the token never stays in the address bar, the history or a Referer header.
        var linkToken = Request.Query[SurveyRespondentIdentityFactory.ResumeQueryKey].ToString();

        if (!string.IsNullOrEmpty(linkToken))
        {
            return await ClaimResumeLinkAsync(responseId, linkToken, confirmed: false);
        }

        var response = await FindOwnAsync(responseId);

        if (response is null)
        {
            return NotFoundPage();
        }

        if (response.Status != SurveyResponseStatus.InProgress)
        {
            return RedirectToAction(nameof(Done), new { responseId });
        }

        var result = await _responses.GetPageAsync(response);

        return await RenderPageAsync(result, message: null, preview && response.IsTest, hidden && response.IsTest);
    }

    /// <summary>
    /// Resumes a response from a resume link once a signed-in visitor confirmed it: the response does not belong to
    /// their account, so they are asked first.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="token">The resume token of the link.</param>
    /// <returns>A redirect to the response, or a page that says why it cannot resume.</returns>
    [HttpPost("s/r/{responseId}/resume")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public Task<IActionResult> Resume(string responseId, string token)
        => string.IsNullOrEmpty(token)
            ? Task.FromResult<IActionResult>(NotFoundPage())
            : ClaimResumeLinkAsync(responseId, token, confirmed: true);

    /// <summary>
    /// Posts the current page: next, back or finish.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="direction">Where the post goes: next, back or finish.</param>
    /// <param name="pageId">The page the respondent posted.</param>
    /// <returns>The next page, the errors, or the ending.</returns>
    [HttpPost("s/r/{responseId}")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Post(string responseId, string direction, string pageId)
    {
        var response = await FindOwnAsync(responseId);

        if (response is null)
        {
            return NotFoundPage();
        }

        var result = await _responses.SubmitPageAsync(new SubmitPageRequest
        {
            ResponseId = responseId,
            Identity = _identities.Build(response.SurveyId),
            PageId = pageId,
            Direction = ParseDirection(direction),
            Form = Request.Form,
        });

        var preview = response.IsTest && string.Equals(Request.Query["preview"], "true", StringComparison.OrdinalIgnoreCase);

        if (result.Ended || result.Error == SurveyErrorCode.DeadlinePassed || result.Response?.Status is SurveyResponseStatus.Completed or SurveyResponseStatus.ScreenedOut)
        {
            return RedirectToAction(nameof(Done), new { responseId });
        }

        if (result.Error == SurveyErrorCode.Busy)
        {
            TempData["SurveyMessage"] = _messages.For(SurveyErrorCode.Busy);

            return RedirectToAction(nameof(Respond), new { responseId, preview });
        }

        if (result.Error == SurveyErrorCode.None)
        {
            // Post, redirect, get: refreshing the next page never posts the previous one again.
            return RedirectToAction(nameof(Respond), new { responseId, preview });
        }

        return await RenderPageAsync(result, _messages.For(result.Error), preview, showHidden: false);
    }

    /// <summary>
    /// Saves the current page without validation.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="pageId">The page.</param>
    /// <returns>The result as JSON.</returns>
    [HttpPost("s/r/{responseId}/save")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Save(string responseId, string pageId)
    {
        var response = await FindOwnAsync(responseId);

        if (response is null)
        {
            return NotFound();
        }

        var result = await _responses.SaveAsync(new SavePageRequest
        {
            ResponseId = responseId,
            Identity = _identities.Build(response.SurveyId),
            PageId = pageId,
            Form = Request.Form,
        });

        return Json(new { ok = result.Succeeded, error = result.Error.ToString(), savedUtc = result.SavedUtc }, _jsonOptions);
    }

    /// <summary>
    /// Evaluates the current page with the answers not yet posted: hidden questions and choices, and piped texts.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The result as JSON.</returns>
    [HttpPost("s/r/{responseId}/evaluate")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Evaluate(string responseId)
    {
        var response = await FindOwnAsync(responseId);

        if (response is null)
        {
            return NotFound();
        }

        var result = await _responses.EvaluateAsync(new EvaluatePageRequest
        {
            ResponseId = responseId,
            Identity = _identities.Build(response.SurveyId),
            Form = Request.Form,
        });

        return Json(new
        {
            ok = result.Succeeded,
            hiddenQuestionIds = result.HiddenQuestionIds,
            hiddenChoiceIds = result.HiddenChoiceIds,
            texts = result.Texts,
        }, _jsonOptions);
    }

    /// <summary>
    /// Checks one answer with immediate feedback and locks it.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="questionId">The question.</param>
    /// <returns>The feedback as JSON.</returns>
    [HttpPost("s/r/{responseId}/check/{questionId}")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Check(string responseId, string questionId)
    {
        var response = await FindOwnAsync(responseId);

        if (response is null)
        {
            return NotFound();
        }

        var result = await _responses.CheckAnswerAsync(new CheckAnswerRequest
        {
            ResponseId = responseId,
            Identity = _identities.Build(response.SurveyId),
            QuestionId = questionId,
            Form = Request.Form,
        });

        return Json(new
        {
            ok = result.Succeeded,
            message = result.Succeeded ? null : _messages.For(result.Error),
            errors = result.Errors,
            points = result.Points,
            feedback = result.Feedback,
        }, _jsonOptions);
    }

    /// <summary>
    /// Shows the ending of a response.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The page or a redirect.</returns>
    [HttpGet("s/r/{responseId}/done")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Done(string responseId)
    {
        var response = await FindOwnAsync(responseId);

        if (response is null)
        {
            return NotFoundPage();
        }

        if (response.Status == SurveyResponseStatus.InProgress)
        {
            return RedirectToAction(nameof(Respond), new { responseId });
        }

        var survey = await _surveyStore.FindByIdAsync(response.SurveyId);
        var version = await _versions.GetAsync(survey, response);

        if (survey is null || version?.Definition is null)
        {
            return NotFoundPage();
        }

        var ending = await _endings.RenderAsync(survey, version.Definition, response, Request.Host.Host);

        if (!string.IsNullOrEmpty(ending.RedirectUrl) && !response.IsTest)
        {
            return Redirect(ending.RedirectUrl);
        }

        var collector = string.IsNullOrEmpty(response.CollectorId) ? null : await _collectorStore.FindByIdAsync(response.CollectorId);
        var definition = version.Definition;
        var shows = ReviewMoment(survey, definition, response, _clock.UtcNow);
        var model = new SurveyEndingViewModel
        {
            Survey = survey,
            Response = response,
            Ending = ending,
            CanReview = response.Status == SurveyResponseStatus.Completed && (shows & ReviewShows.Responses) != 0,
            CanReopen = response.Status == SurveyResponseStatus.Completed && definition.Navigation?.AllowEditAfterSubmit == true && survey.Status == SurveyStatus.Open,
            ShowScore = definition.Scoring?.Enabled == true && definition.Scoring.ShowScoreToRespondent && (shows & ReviewShows.Score) != 0 && response.Grading == GradingStatus.Graded,
            // A response started without a collector (a course item's survey) reaches the totals through the response itself.
            ResultsUrl = definition.Review?.ResultsVisibility is not (ResultsVisibility.AfterAnswer or ResultsVisibility.Always)
                ? null
                : collector is not null
                    ? Url.Content("~/s/" + collector.Token + "/results")
                    : response.Status == SurveyResponseStatus.Completed ? Url.Content("~/s/r/" + response.ItemId + "/results") : null,
        };

        // A consumer that started the response (a course item) may ask for a link back; only a local URL is followed.
        var returnUrl = SurveyResponseProperties.ReadString(response, SurveyResponseProperties.ReturnUrl);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            model.ReturnUrl = returnUrl;
            model.ReturnText = SurveyResponseProperties.ReadString(response, SurveyResponseProperties.ReturnText);
        }

        if (collector?.Kind == SurveyCollectorKinds.Kiosk)
        {
            _identities.ClearResumeCookie(survey.ItemId);
            model.KioskToken = collector.Token;
            model.KioskResetSeconds = Math.Max(collector.KioskResetSeconds, 3);
            model.CanReview = false;
            model.CanReopen = false;
        }

        return View("Done", model);
    }

    /// <summary>
    /// Reopens a completed response so the respondent can change it.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>A redirect to the response.</returns>
    [HttpPost("s/r/{responseId}/reopen")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Reopen(string responseId)
    {
        var response = await FindOwnAsync(responseId);

        if (response is null)
        {
            return NotFoundPage();
        }

        await _responses.ReopenAsync(responseId, _identities.Build(response.SurveyId));

        return RedirectToAction(nameof(Respond), new { responseId });
    }

    /// <summary>
    /// Shows the review of a completed response, as its review settings allow.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The page.</returns>
    [HttpGet("s/r/{responseId}/review")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Review(string responseId)
    {
        var response = await FindOwnAsync(responseId);

        if (response is null || response.Status != SurveyResponseStatus.Completed)
        {
            return NotFoundPage();
        }

        var survey = await _surveyStore.FindByIdAsync(response.SurveyId);
        var version = await _versions.GetAsync(survey, response);

        if (survey is null || version?.Definition is null)
        {
            return NotFoundPage();
        }

        var shows = ReviewMoment(survey, version.Definition, response, _clock.UtcNow);

        if ((shows & ReviewShows.Responses) == 0)
        {
            return MessagePage(S["Review"], S["The review of this response is not available."]);
        }

        return View("Review", new SurveyReviewViewModel
        {
            Survey = survey,
            Title = string.IsNullOrWhiteSpace(version.Definition.Title) ? survey.Title : version.Definition.Title,
            Response = response,
            Shows = version.Definition.Scoring?.ShowScoreToRespondent == false ? shows & ~ReviewShows.Score : shows,
            Questions = await _shapes.BuildReviewAsync(survey, version, response, shows),
        });
    }

    /// <summary>
    /// Shows the totals of a survey's choice questions to respondents, when the survey allows it.
    /// </summary>
    /// <param name="token">The collector token.</param>
    /// <returns>The page.</returns>
    [HttpGet("s/{token}/results")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> Results(string token)
    {
        var (survey, _) = await FindByTokenAsync(token);

        if (survey is null)
        {
            return NotFoundPage();
        }

        return await ResultsCoreAsync(survey, () => HasAnsweredAsync(survey));
    }

    /// <summary>
    /// Shows the totals of a survey's choice questions to the respondent of one of its responses, for a survey reached
    /// without a public link (a course item's survey), when the survey allows it.
    /// </summary>
    /// <param name="responseId">The respondent's own response.</param>
    /// <returns>The page.</returns>
    [HttpGet("s/r/{responseId}/results")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> ResponseResults(string responseId)
    {
        var response = await FindOwnAsync(responseId);
        var survey = response is null ? null : await _surveyStore.FindByIdAsync(response.SurveyId);

        if (survey is null)
        {
            return NotFoundPage();
        }

        return await ResultsCoreAsync(survey, () => Task.FromResult(response.Status == SurveyResponseStatus.Completed));
    }

    private async Task<IActionResult> ResultsCoreAsync(Survey survey, Func<Task<bool>> hasAnswered)
    {
        var version = await _versionCache.GetPublishedAsync(survey);
        var visibility = version?.Definition?.Review?.ResultsVisibility ?? ResultsVisibility.Never;
        var closed = survey.Status is SurveyStatus.Closed or SurveyStatus.Archived;
        var allowed = visibility == ResultsVisibility.Always
            || (visibility == ResultsVisibility.AfterClose && closed)
            || (visibility == ResultsVisibility.AfterAnswer && await hasAnswered());

        var aggregates = _serviceProvider.GetService<ISurveyAnswerAggregates>();

        if (!allowed || aggregates is null)
        {
            return MessagePage(survey.Title, S["The results of this survey are not available."]);
        }

        var filter = new SurveyAnswerFilter { SurveyId = survey.ItemId };
        var model = new SurveyResultsViewModel
        {
            Survey = survey,
            Title = string.IsNullOrWhiteSpace(version.Definition.Title) ? survey.Title : version.Definition.Title,
            Responses = await aggregates.CountResponsesAsync(filter),
        };

        if (survey.Access?.Anonymity == AnonymityMode.Anonymous && model.Responses < Math.Max(survey.Access.MinimumGroupSize, 0))
        {
            return MessagePage(survey.Title, S["The results show once enough people have responded."]);
        }

        var registry = _serviceProvider.GetService<ISurveyControlRegistry>();

        foreach (var question in version.Definition.Pages.SelectMany(page => page.Questions ?? []).Where(question => question.Choices?.Count > 0 && question.Rows?.Count == 0))
        {
            var counts = await aggregates.CountChoicesAsync(filter, question.Id);
            var row = counts.TryGetValue(string.Empty, out var values) ? values : new Dictionary<string, long>();
            var total = Math.Max(row.Values.Sum(), 1);
            var choices = question.Choices.Where(choice => !choice.Retired);

            if (registry?.GetScorer(question.ControlType)?.AnswerKeyIsOrder == true)
            {
                // The designed order is the answer key of this type (a ranking), so the choices are listed by text.
                choices = choices.OrderBy(choice => choice.Text, StringComparer.CurrentCultureIgnoreCase).ThenBy(choice => choice.Id, StringComparer.Ordinal);
            }

            model.Questions.Add(new SurveyResultsQuestion
            {
                Title = question.Title,
                Choices = choices
                    .Select(choice => (choice.Text, row.TryGetValue(choice.Id, out var count) ? count : 0L, Math.Round((row.TryGetValue(choice.Id, out var c) ? c : 0L) * 100m / total, 1)))
                    .ToList(),
            });
        }

        return View("Results", model);
    }

    /// <summary>
    /// Lists the signed-in respondent's own responses with links to continue or review them.
    /// </summary>
    /// <returns>The page.</returns>
    [HttpGet("surveys/my")]
    [RateLimitGroup(RespondRateLimitsStartup.RateLimitGroup)]
    public async Task<IActionResult> My()
    {
        var identity = _identities.Build(null);

        if (string.IsNullOrEmpty(identity.UserId))
        {
            return Challenge();
        }

        var model = new MySurveyResponsesViewModel();
        var responses = await _counter.ListForUserAsync(identity.UserId, SurveysConstants.PageSize);
        var surveys = (await _surveyStore.GetAsync(responses.Select(response => response.SurveyId).Distinct(StringComparer.Ordinal)))
            .ToDictionary(survey => survey.ItemId, StringComparer.Ordinal);

        foreach (var response in responses)
        {
            if (surveys.TryGetValue(response.SurveyId, out var survey))
            {
                model.Items.Add((response, survey));
            }
        }

        return View("My", model);
    }

    private async Task<IActionResult> StartCoreAsync(string token, Survey survey, SurveyCollector collector, string password, bool captchaPassed)
    {
        var identity = _identities.Build(survey.ItemId, ensureBrowserKey: survey.Access?.Limits?.Uniqueness == UniquenessMode.Cookie);

        identity.CaptchaPassed = captchaPassed;

        if (collector.Kind == SurveyCollectorKinds.Kiosk)
        {
            identity.ResumeToken = null;
            identity.BrowserKey = null;
        }

        var query = Request.Query
            .Where(pair => !_reservedQueryKeys.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);

        var result = await _responses.StartAsync(new StartResponseRequest
        {
            CollectorToken = token,
            Identity = identity,
            QueryVariables = query,
            Password = password,
            Culture = RespondentCulture(collector),
        });

        if (!result.Succeeded)
        {
            var model = await LandingModelAsync(token, survey, collector);
            model.Message = _messages.For(result.Error);
            model.NeedsPassword |= result.Error is SurveyErrorCode.PasswordRequired or SurveyErrorCode.PasswordInvalid;
            model.NeedsCaptcha |= result.Error == SurveyErrorCode.CaptchaRequired;
            model.CanStart = result.Error is SurveyErrorCode.PasswordRequired or SurveyErrorCode.PasswordInvalid or SurveyErrorCode.CaptchaRequired or SurveyErrorCode.Busy;

            return View("Landing", model);
        }

        if (!string.IsNullOrEmpty(result.ResumeToken))
        {
            _identities.WriteResumeCookie(survey.ItemId, result.ResumeToken);
        }

        return RedirectToAction(nameof(Respond), new { responseId = result.Response.ItemId });
    }

    // The culture a respondent sees: one asked for on the link (?culture=), else the collector's, else the request's.
    private string RespondentCulture(SurveyCollector collector)
    {
        var asked = Request.Query[CultureQueryKey].ToString();

        if (!string.IsNullOrWhiteSpace(asked) && asked.Length <= 20)
        {
            try
            {
                return CultureInfo.GetCultureInfo(asked.Trim()).Name;
            }
            catch (CultureNotFoundException)
            {
                // An unknown culture on the link is ignored.
            }
        }

        return !string.IsNullOrWhiteSpace(collector?.Culture) ? collector.Culture : CultureInfo.CurrentUICulture.Name;
    }

    private async Task<SurveyLandingViewModel> LandingModelAsync(string token, Survey survey, SurveyCollector collector)
    {
        var version = await _versionCache.GetPublishedAsync(survey);
        var definition = version?.Definition;
        var culture = RespondentCulture(collector);
        var title = SurveyDefinitionLocalizer.Translate(definition, culture, SurveyTextKeys.SurveyTitle, definition?.Title);

        return new SurveyLandingViewModel
        {
            Token = token,
            Survey = survey,
            Title = string.IsNullOrWhiteSpace(title) ? survey.Title : title,
            Introduction = SurveyDefinitionLocalizer.Translate(definition, culture, SurveyTextKeys.SurveyIntroduction, definition?.Introduction),
            NeedsPassword = !string.IsNullOrEmpty(collector?.PasswordHash),
            NeedsCaptcha = survey.Access?.RequireCaptcha == true && User?.Identity?.IsAuthenticated != true && _serviceProvider.GetService<ReCaptchaService>() is not null,
            CanStart = survey.Status == SurveyStatus.Open && definition is not null && collector?.Status != CollectorStatus.Closed,
            Message = survey.Status == SurveyStatus.Open ? null : _messages.For(survey.Status == SurveyStatus.Draft ? SurveyErrorCode.NotOpen : SurveyErrorCode.Closed),
            QueryString = Request.QueryString.Value,
            ShowResultsLink = definition?.Review?.ResultsVisibility == ResultsVisibility.Always,
            SkipLanding = definition?.Presentation?.SkipLanding == true,
        };
    }

    private async Task<IActionResult> RenderPageAsync(SurveyPageResult result, string message, bool preview, bool showHidden)
    {
        if (result.Response is null || result.Page is null)
        {
            return result.Ended && result.Response is not null
                ? RedirectToAction(nameof(Done), new { responseId = result.Response.ItemId })
                : NotFoundPage();
        }

        var survey = await _surveyStore.FindByIdAsync(result.Response.SurveyId);
        var version = await _versions.GetAsync(survey, result.Response);

        if (survey is null || version is null)
        {
            return NotFoundPage();
        }

        message ??= TempData["SurveyMessage"] as string;

        var shape = await _shapes.BuildPageAsync(survey, version, result.Response, result.Page, result.Errors, message, preview, showHidden);

        ViewData["SurveyClientRules"] = JsonSerializer.Serialize(result.Page.ClientRules, _ruleOptions);

        return View("Respond", shape);
    }

    private ReviewShows ReviewMoment(Survey survey, SurveyDefinition definition, SurveyResponse response, DateTime utcNow)
    {
        if (_serviceProvider.GetService<ISurveyReviewPolicy>() is { } policy)
        {
            return policy.GetAllowed(survey, definition, response, utcNow);
        }

        var review = definition.Review ?? new SurveyReviewSettings();
        var closed = survey.Status is SurveyStatus.Closed or SurveyStatus.Archived;

        if (closed)
        {
            return review.AfterClose;
        }

        return response.CompletedUtc is { } completed && (utcNow - completed) < TimeSpan.FromMinutes(10)
            ? review.ImmediatelyAfter
            : review.LaterWhileOpen;
    }

    private async Task<bool> HasAnsweredAsync(Survey survey)
    {
        var identity = _identities.Build(survey.ItemId);
        var responseId = SurveyResumeTokens.ResponseIdOf(identity.ResumeToken);

        if (!string.IsNullOrEmpty(responseId) && await _responses.FindForRespondentAsync(responseId, identity) is { Status: SurveyResponseStatus.Completed })
        {
            return true;
        }

        return !string.IsNullOrEmpty(identity.UserId)
            && (await _counter.ListForUserAsync(identity.UserId, SurveysConstants.PageSize)).Any(response => response.SurveyId == survey.ItemId && response.Status == SurveyResponseStatus.Completed);
    }

    // Trades the token of a resume link for a new one in this browser's cookie. The link resumes only its own response,
    // only while its token is the response's current one (so once), never replaces the cookie of another response this
    // browser has open, and asks a signed-in visitor before resuming a response that is not their account's.
    private async Task<IActionResult> ClaimResumeLinkAsync(string responseId, string linkToken, bool confirmed)
    {
        if (string.IsNullOrEmpty(responseId)
            || responseId.Length > 64
            || !string.Equals(SurveyResumeTokens.ResponseIdOf(linkToken), responseId, StringComparison.Ordinal))
        {
            return NotFoundPage();
        }

        var response = await _responseStore.FindByIdAsync(responseId);

        if (response is null)
        {
            return NotFoundPage();
        }

        var identity = _identities.Build(response.SurveyId);
        var cleanUrl = Url.Content("~/s/r/" + Uri.EscapeDataString(responseId)) + QueryWithoutResumeToken();

        // This browser holds the response already (the same link opened twice): nothing to trade.
        if (SurveyResponseService.IsOwner(response, identity))
        {
            return Redirect(cleanUrl);
        }

        if (string.IsNullOrEmpty(response.ResumeTokenHash) || !SurveyResumeTokens.Matches(linkToken, response.ResumeTokenHash))
        {
            return NotFoundPage();
        }

        var heldId = SurveyResumeTokens.ResponseIdOf(identity.ResumeToken);

        if (!string.IsNullOrEmpty(heldId)
            && !string.Equals(heldId, responseId, StringComparison.Ordinal)
            && await _responses.FindForRespondentAsync(heldId, identity) is { Status: SurveyResponseStatus.InProgress })
        {
            Response.StatusCode = StatusCodes.Status409Conflict;

            return MessagePage(S["Survey"], S["This browser already has a response in progress for this survey. Finish it before you open another one."]);
        }

        if (!confirmed && !string.IsNullOrEmpty(identity.UserId) && string.IsNullOrEmpty(response.UserId))
        {
            var survey = await _surveyStore.FindByIdAsync(response.SurveyId);

            return View("Resume", new SurveyResumeViewModel
            {
                ResponseId = responseId,
                Token = linkToken,
                Title = survey?.Title ?? S["Survey"].Value,
                QueryString = QueryWithoutResumeToken(),
            });
        }

        var token = await _responses.RotateResumeTokenAsync(responseId, linkToken, HttpContext.RequestAborted);

        if (string.IsNullOrEmpty(token))
        {
            return NotFoundPage();
        }

        _identities.WriteResumeCookie(response.SurveyId, token);

        return Redirect(cleanUrl);
    }

    private string QueryWithoutResumeToken()
        => QueryString.Create(Request.Query
            .Where(pair => !string.Equals(pair.Key, SurveyRespondentIdentityFactory.ResumeQueryKey, StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value))))
            .ToUriComponent();

    private async Task<(Survey Survey, SurveyCollector Collector)> FindByTokenAsync(string token)
    {
        var collector = await _collectorStore.FindByTokenAsync(token);
        var survey = collector is null ? null : await _surveyStore.FindByIdAsync(collector.SurveyId);

        return (survey, collector);
    }

    private async Task<SurveyResponse> FindOwnAsync(string responseId)
    {
        if (string.IsNullOrEmpty(responseId) || responseId.Length > 64)
        {
            return null;
        }

        var response = await _responseStore.FindByIdAsync(responseId);

        return response is not null && SurveyResponseService.IsOwner(response, _identities.Build(response.SurveyId)) ? response : null;
    }

    private static SurveyPageDirection ParseDirection(string direction)
        => direction?.ToLowerInvariant() switch
        {
            "back" => SurveyPageDirection.Back,
            "finish" => SurveyPageDirection.Finish,
            _ => SurveyPageDirection.Next,
        };

    private ViewResult NotFoundPage()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;

        return MessagePage(S["Survey"], _messages.For(SurveyErrorCode.NotFound));
    }

    private ViewResult MessagePage(string title, string message)
        => View("Message", new SurveyMessageViewModel { Title = title, Message = message });
}
