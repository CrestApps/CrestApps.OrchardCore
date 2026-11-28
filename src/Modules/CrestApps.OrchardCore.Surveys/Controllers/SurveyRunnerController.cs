using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Filters;
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
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The respondent pages: the landing page of a link, the pages of a response, autosave, live evaluation, immediate
/// feedback, the ending, the review, the totals of a poll and the respondent's own responses. The controller is thin:
/// the response service decides everything.
/// </summary>
[TypeFilter(typeof(SurveyAnonymitySecretUnreadableFilter))]
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

    /// <summary>
    /// The query string key that asks the landing page to start at once, as the Start button of an embedded survey
    /// does (the embed may be cached, so it links here instead of posting a form with a form token).
    /// </summary>
    public const string AutoStartQueryKey = "autostart";

    /// <summary>
    /// How long a kiosk response page may be left without input before it goes back to the landing page.
    /// </summary>
    public const int KioskIdleSeconds = 180;

    /// <summary>
    /// The header the runner script sends with a page it posts itself; the answer is then JSON naming where to go.
    /// </summary>
    public const string FetchHeader = "X-Survey-Fetch";

    private const string ErrorsTempDataKey = "SurveyErrors";

    private static readonly HashSet<string> _reservedQueryKeys = new(StringComparer.OrdinalIgnoreCase) { "preview", "hidden", CultureQueryKey, AutoStartQueryKey };

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

            if (IsSignedIn())
            {
                RefuseSignedInKiosk(model);
            }
        }
        else if (model.CanStart && !model.NeedsPassword && !model.NeedsCaptcha
            && (model.SkipLanding || IsTrustedAutoStart(Request)))
        {
            model.AutoStart = true;
        }

        return View("Landing", model);
    }

    /// <summary>
    /// Gets whether a request may start a survey at once through <c>?autostart=1</c>: only a navigation from a page of
    /// the same site (the Start button of an embedded survey) does, as the browser's <c>Sec-Fetch-Site</c> header tells.
    /// A link on another site, in an e-mail or typed in shows the landing page, its introduction and its Start button,
    /// so no outside link can skip the introduction or use up an attempt.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when the landing page posts its start form at once.</returns>
    internal static bool IsTrustedAutoStart(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.Equals(request.Query[AutoStartQueryKey], "1", StringComparison.Ordinal)
            && string.Equals(request.Headers["Sec-Fetch-Site"], "same-origin", StringComparison.OrdinalIgnoreCase);
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
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
    public async Task<IActionResult> Respond(string responseId, bool preview = false, bool hidden = false)
    {
        // There is no resume link: a response resumes only for its signed-in user or the browser holding its resume
        // cookie, so no address can hand a response (and the answers in it) to another browser.
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

        // The errors of a page the runner script posted, carried over its redirect.
        if (TempData[ErrorsTempDataKey] is string errors && result.Page is not null)
        {
            result.Errors = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(errors) ?? [];
        }

        return await RenderPageAsync(result, message: null, preview && response.IsTest, hidden && response.IsTest);
    }

    /// <summary>
    /// Posts the current page: next, back or finish.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <param name="direction">Where the post goes: next, back or finish.</param>
    /// <param name="pageId">The page the respondent posted.</param>
    /// <returns>The next page, the errors, or the ending.</returns>
    [HttpPost("s/r/{responseId}")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
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

        // The runner script posts the page itself, so it can keep the answers and send them again when the server is
        // busy (429): it is told where to go, and the errors of a refused page go there with it.
        var fromScript = string.Equals(Request.Headers[FetchHeader], "1", StringComparison.Ordinal);

        IActionResult Go(string action, object values)
            => fromScript ? Json(new { redirect = Url.Action(action, values) }, _jsonOptions) : RedirectToAction(action, values);

        if (result.Ended || result.Error is SurveyErrorCode.DeadlinePassed or SurveyErrorCode.Closed || result.Response?.Status is SurveyResponseStatus.Completed or SurveyResponseStatus.ScreenedOut)
        {
            return Go(nameof(Done), new { responseId });
        }

        if (result.Error == SurveyErrorCode.Busy)
        {
            TempData["SurveyMessage"] = _messages.For(SurveyErrorCode.Busy);

            return Go(nameof(Respond), new { responseId, preview });
        }

        if (result.Error == SurveyErrorCode.None)
        {
            // Post, redirect, get: refreshing the next page never posts the previous one again.
            return Go(nameof(Respond), new { responseId, preview });
        }

        if (fromScript && result.Page is not null)
        {
            TempData["SurveyMessage"] = _messages.For(result.Error);
            TempData[ErrorsTempDataKey] = JsonSerializer.Serialize(result.Errors ?? []);

            return Go(nameof(Respond), new { responseId, preview });
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
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
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

        // A refused save (answers that would skip or hide an answer already checked) saved nothing and says why.
        return Json(new { ok = result.Succeeded, error = result.Error.ToString(), savedUtc = result.SavedUtc, errors = result.Errors }, _jsonOptions);
    }

    /// <summary>
    /// Evaluates the current page with the answers not yet posted: hidden questions and choices, and piped texts.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The result as JSON.</returns>
    [HttpPost("s/r/{responseId}/evaluate")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
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

            // A title and a description are HTML: they go out sanitized, as the page shows them. The other texts are plain.
            texts = result.Texts.ToDictionary(pair => pair.Key, pair => SurveyPageViewBuilder.IsHtmlText(pair.Key) ? Sanitize(pair.Value) : pair.Value, StringComparer.Ordinal),
            errors = result.Errors,
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
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
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

        // Feedback is the author's HTML: it goes out sanitized, and the page shows it as markup, not as text.
        if (result.Feedback is { } feedback)
        {
            feedback.Feedback = Sanitize(feedback.Feedback);
            feedback.GeneralFeedback = Sanitize(feedback.GeneralFeedback);
        }

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
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
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

        var collector = string.IsNullOrEmpty(response.CollectorId) ? null : await _collectorStore.FindByIdAsync(response.CollectorId);

        // An abandoned or voided response was never received: it doesn't show an ending that thanks the respondent.
        if (response.Status is SurveyResponseStatus.Abandoned or SurveyResponseStatus.Voided)
        {
            if (collector?.Kind == SurveyCollectorKinds.Kiosk)
            {
                _identities.ClearResumeCookie(survey.ItemId);
            }
            else
            {
                _identities.ShortenResumeCookie(survey.ItemId);
            }

            return MessagePage(survey.Title, response.Status == SurveyResponseStatus.Abandoned
                ? S["This response was not submitted: it was left unfinished, or it could no longer be submitted. Your answers were not received as a response."]
                : S["This response was withdrawn and doesn't count."]);
        }

        // The same test of a closed survey as reopening: staff closed it or its link, or its closing date passed.
        var closed = await IsClosedForAsync(survey, collector, response);

        // A kiosk forgets the response before anything else, a redirecting ending included, so the next person at the
        // device never reaches it.
        if (collector?.Kind == SurveyCollectorKinds.Kiosk)
        {
            _identities.ClearResumeCookie(survey.ItemId);
        }
        else if (CanReturnLater(survey, version.Definition, response, closed))
        {
            // A guest's only proof that the response is theirs is this cookie, so while they may still reopen or review
            // it later it keeps (and renews) its full life.
            var resumeToken = _identities.Build(survey.ItemId).ResumeToken;

            if (!string.IsNullOrEmpty(resumeToken))
            {
                _identities.WriteResumeCookie(survey.ItemId, resumeToken);
            }
        }
        else
        {
            // Otherwise the ended response keeps its cookie a day for the review shown right after it, not the full 30
            // days, so cookies don't pile up.
            _identities.ShortenResumeCookie(survey.ItemId);
        }

        var ending = await _endings.RenderAsync(survey, version.Definition, response, Request.Host.Host);

        if (!string.IsNullOrEmpty(ending.RedirectUrl) && !response.IsTest)
        {
            return Redirect(ending.RedirectUrl);
        }

        var definition = version.Definition;
        var shows = ReviewMoment(survey, definition, response, _clock.UtcNow);
        var model = new SurveyEndingViewModel
        {
            Survey = survey,
            Response = response,
            Ending = ending,
            CanReview = response.Status == SurveyResponseStatus.Completed && (shows & ReviewShows.Responses) != 0,
            CanReopen = response.Status == SurveyResponseStatus.Completed && definition.Navigation?.AllowEditAfterSubmit == true && survey.Status == SurveyStatus.Open && !closed,
            ShowScore = definition.Scoring?.Enabled == true && definition.Scoring.ShowScoreToRespondent && (shows & ReviewShows.Score) != 0 && response.Grading == GradingStatus.Graded,
            ResultsUrl = ResultsLink(definition, collector, response),
            Message = TempData["SurveyMessage"] as string,
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
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
    public async Task<IActionResult> Reopen(string responseId)
    {
        var response = await FindOwnAsync(responseId);

        // A kiosk response belongs to whoever stood at the device, so the next person can't reopen it.
        if (response is null || await IsKioskAsync(response))
        {
            return NotFoundPage();
        }

        var identity = _identities.Build(response.SurveyId);

        var reopened = await _responses.ReopenAsync(responseId, identity);

        // A refused reopen (the survey doesn't allow changes after submitting, it closed, or the response is busy) says
        // why on the page the respondent came from, instead of quietly showing the ending again.
        if (reopened.Error != SurveyErrorCode.None)
        {
            TempData["SurveyMessage"] = _messages.For(reopened.Error);

            return RedirectToAction(nameof(Done), new { responseId });
        }

        // The ending shortened the resume cookie; a response in progress again gets its full life back.
        if (!string.IsNullOrEmpty(identity.ResumeToken))
        {
            _identities.WriteResumeCookie(response.SurveyId, identity.ResumeToken);
        }

        return RedirectToAction(nameof(Respond), new { responseId });
    }

    /// <summary>
    /// Shows the review of a completed response, as its review settings allow.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The page.</returns>
    [HttpGet("s/r/{responseId}/review")]
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
    public async Task<IActionResult> Review(string responseId)
    {
        var response = await FindOwnAsync(responseId);

        // A kiosk response belongs to whoever stood at the device, so the next person can't review it.
        if (response is null || response.Status != SurveyResponseStatus.Completed || await IsKioskAsync(response))
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
            Questions = await _shapes.BuildReviewAsync(survey, version, response, shows, forRespondent: true),
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
    [RateLimitGroup(RespondRateLimitsStartup.ResponseRateLimitGroup, RespondRateLimitsStartup.ClientCeilingGroup)]
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

    private string ResultsLink(SurveyDefinition definition, SurveyCollector collector, SurveyResponse response)
        => ResultsPath(definition, collector, response) is { } path ? Url.Content(path) : null;

    /// <summary>
    /// Gets the app-relative path of the totals the Done page links to, or <see langword="null"/> when it shows no link.
    /// A public link's own totals page serves its respondents. A response started without a collector (a course item's
    /// survey) or through an invitation reaches the totals through the response itself: an invitation collector's token
    /// is not given to the invitee, who could otherwise pass it around as a public link.
    /// </summary>
    /// <param name="definition">The published definition.</param>
    /// <param name="collector">The collector the response came through, or <see langword="null"/>.</param>
    /// <param name="response">The response.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    internal static string ResultsPath(SurveyDefinition definition, SurveyCollector collector, SurveyResponse response)
    {
        if (definition?.Review?.ResultsVisibility is not (ResultsVisibility.AfterAnswer or ResultsVisibility.Always))
        {
            return null;
        }

        if (collector is not null && collector.Kind != SurveyCollectorKinds.Invitation)
        {
            return "~/s/" + collector.Token + "/results";
        }

        return response?.Status == SurveyResponseStatus.Completed ? "~/s/r/" + response.ItemId + "/results" : null;
    }

    private async Task<IActionResult> ResultsCoreAsync(Survey survey, Func<Task<bool>> hasAnswered)
    {
        var version = await _versionCache.GetPublishedAsync(survey);
        var visibility = version?.Definition?.Review?.ResultsVisibility ?? ResultsVisibility.Never;

        // The survey closes when staff close it, or when its closing date and the grace past it are over.
        var closed = Core.Access.SurveyAvailability.IsClosedForReview(survey, version?.Definition, _clock.UtcNow);
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

        // The survey's minimum group size, never below the site's floor, applies to the total and to each question: a
        // question few people answered would show those few people's choices.
        var siteSettings = _serviceProvider.GetService<ISiteService>() is { } siteService
            ? await siteService.GetSettingsAsync<SurveySettings>()
            : null;

        if (Core.Access.SurveyAnonymity.IsTooSmall(survey, siteSettings, model.Responses))
        {
            return MessagePage(survey.Title, S["The results show once enough people have responded."]);
        }

        var registry = _serviceProvider.GetService<ISurveyControlRegistry>();

        foreach (var question in version.Definition.Pages.SelectMany(page => page.Questions ?? []).Where(question => question.Choices?.Count > 0 && question.Rows?.Count == 0))
        {
            if (Core.Access.SurveyAnonymity.IsAnonymous(survey) && Core.Access.SurveyAnonymity.IsTooSmall(survey, siteSettings, await aggregates.CountAnsweredAsync(filter, question.Id)))
            {
                continue;
            }

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
                // The respondent sees the version they answered, never the author's unpublished draft.
                var title = (await _versions.GetAsync(survey, response))?.Definition?.Title;

                model.Items.Add((response, survey, string.IsNullOrWhiteSpace(title) ? survey.Title : title));
            }
        }

        return View("My", model);
    }

    private async Task<IActionResult> StartCoreAsync(string token, Survey survey, SurveyCollector collector, string password, bool captchaPassed)
    {
        // A kiosk starts guest responses that no per-person limit counts, so a signed-in visitor (someone who opened the
        // kiosk address at home) is refused instead: answering as a guest there would bypass the survey's limits.
        if (collector.Kind == SurveyCollectorKinds.Kiosk && IsSignedIn())
        {
            var refused = await LandingModelAsync(token, survey, collector);

            RefuseSignedInKiosk(refused);

            return View("Landing", refused);
        }

        var identity = _identities.Build(survey.ItemId, ensureBrowserKey: survey.Access?.Limits?.Uniqueness == UniquenessMode.Cookie);

        identity.CaptchaPassed = captchaPassed;

        // A kiosk is a shared device: each start is a new guest, so a response never belongs to anyone (which would
        // resume it for the next person).
        if (collector.Kind == SurveyCollectorKinds.Kiosk)
        {
            identity.ResumeToken = null;
            identity.BrowserKey = null;
            identity.UserId = null;
            identity.Party = null;
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
            CssClass = definition?.Presentation?.CssClass,
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

        // A kiosk is a shared device: a response page left alone goes back to the landing page (which forgets the
        // response), so the next person never sees the previous person's answers.
        if (!string.IsNullOrEmpty(result.Response.CollectorId)
            && await _collectorStore.FindByIdAsync(result.Response.CollectorId) is { Kind: SurveyCollectorKinds.Kiosk } kiosk)
        {
            ViewData["SurveyKioskIdleUrl"] = Url.Content("~/s/" + kiosk.Token);
            ViewData["SurveyKioskIdleSeconds"] = KioskIdleSeconds;
        }

        return View("Respond", shape);
    }

    // Whether the respondent may still come back to an ended response after the short cookie life would end: reopen it
    // (edit after submit while the survey is open) or review its answers (the review shown later while the survey is
    // open, or the one after it closes). An open survey closes on its close date or whenever staff close it, so a review
    // after close that shows the answers keeps the cookie too; a closed one already uses that review.
    internal static bool CanReturnLater(Survey survey, SurveyDefinition definition, SurveyResponse response, ReviewShows later, bool closed = false)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        if (response.Status != SurveyResponseStatus.Completed)
        {
            return false;
        }

        if (definition.Navigation?.AllowEditAfterSubmit == true && survey.Status == SurveyStatus.Open && !closed)
        {
            return true;
        }

        if ((later & ReviewShows.Responses) != 0)
        {
            return true;
        }

        // A completed response is never voided or abandoned by its respondent, so what the review after close shows of
        // its answers is the setting itself.
        return survey.Status == SurveyStatus.Open && !closed
            && ((definition.Review ?? new SurveyReviewSettings()).AfterClose & ReviewShows.Responses) != 0;
    }

    private bool CanReturnLater(Survey survey, SurveyDefinition definition, SurveyResponse response, bool closed)
        => CanReturnLater(survey, definition, response, ReviewMoment(survey, definition, response, _clock.UtcNow + SurveyResumeCookies.CompletedMaxAge), closed);

    // Whether the survey is closed to the respondent of a response, as the response service decides it.
    private async Task<bool> IsClosedForAsync(Survey survey, SurveyCollector collector, SurveyResponse response)
    {
        if (response.IsTest)
        {
            return false;
        }

        // The survey's own closing date counts for reopening even with an accommodation that closes later: the review
        // after close follows the survey's date.
        if (Core.Access.SurveyAvailability.IsClosed(survey, surveyOverride: null, collector, _clock.UtcNow))
        {
            return true;
        }

        SurveyOverride surveyOverride = null;

        if (survey.Access?.ClosesUtc is not null && _serviceProvider.GetService<ISurveyOverrideService>() is { } overrides)
        {
            surveyOverride = await overrides.FindEffectiveAsync(survey.ItemId, response.UserId ?? _identities.Build(survey.ItemId).UserId, response.Context);
        }

        return Core.Access.SurveyAvailability.IsClosed(survey, surveyOverride, collector, _clock.UtcNow);
    }

    private ReviewShows ReviewMoment(Survey survey, SurveyDefinition definition, SurveyResponse response, DateTime utcNow)
    {
        if (_serviceProvider.GetService<ISurveyReviewPolicy>() is { } policy)
        {
            return policy.GetAllowed(survey, definition, response, utcNow);
        }

        var review = definition.Review ?? new SurveyReviewSettings();
        var closed = Core.Access.SurveyAvailability.IsClosedForReview(survey, definition, utcNow);

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

        // The resume cookie is named after the survey, but its value is the respondent's: a completed response of another
        // survey, copied under this survey's name, opens nothing here.
        if (!string.IsNullOrEmpty(responseId)
            && await _responses.FindForRespondentAsync(responseId, identity) is { Status: SurveyResponseStatus.Completed } response
            && string.Equals(response.SurveyId, survey.ItemId, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrEmpty(identity.UserId)
            && (await _counter.ListForUserAsync(identity.UserId, SurveysConstants.PageSize)).Any(response => response.SurveyId == survey.ItemId && response.Status == SurveyResponseStatus.Completed);
    }

    private async Task<(Survey Survey, SurveyCollector Collector)> FindByTokenAsync(string token)
    {
        var collector = await _collectorStore.FindByTokenAsync(token);
        var survey = collector is null ? null : await _surveyStore.FindByIdAsync(collector.SurveyId);

        return (survey, collector);
    }

    private async Task<bool> IsKioskAsync(SurveyResponse response)
        => !string.IsNullOrEmpty(response.CollectorId)
            && await _collectorStore.FindByIdAsync(response.CollectorId) is { Kind: SurveyCollectorKinds.Kiosk };

    private async Task<SurveyResponse> FindOwnAsync(string responseId)
    {
        if (string.IsNullOrEmpty(responseId) || responseId.Length > 64)
        {
            return null;
        }

        var response = await _responseStore.FindByIdAsync(responseId);

        return response is not null && SurveyResponseService.IsOwner(response, _identities.Build(response.SurveyId)) ? response : null;
    }

    private string Sanitize(string html)
        => string.IsNullOrEmpty(html) || _serviceProvider.GetService<SurveyContentSanitizer>() is not { } sanitizer ? html : sanitizer.Sanitize(html);

    private bool IsSignedIn()
        => User?.Identity?.IsAuthenticated == true;

    private void RefuseSignedInKiosk(SurveyLandingViewModel model)
    {
        model.CanStart = false;
        model.AutoStart = false;
        model.Message = S["This survey is set up for a shared device and records guest answers. Sign out to answer it here, or use the link you were given for your own answers."];
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
