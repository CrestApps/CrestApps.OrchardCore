using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The responses of a survey for staff: the list, one response, voiding and deleting, and the preview of a survey.
/// </summary>
[Admin]
public sealed class SurveyResponsesController : Controller
{
    private const string VoidAction = "Void";
    private const string DeleteAction = "Delete";

    private readonly SurveyStore _surveyStore;
    private readonly SurveyResponseStore _responseStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly ISurveyVersionCache _versionCache;
    private readonly SurveyRuntimeVersions _versions;
    private readonly ISurveyResponseService _responses;
    private readonly SurveyRunnerShapes _shapes;
    private readonly SurveyRespondentIdentityFactory _identities;
    private readonly IDisplayManager<SurveyResponse> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISession _session;
    private readonly INotifier _notifier;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStringLocalizer S;
    private readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponsesController"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="responseStore">The response store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="versionCache">The version cache.</param>
    /// <param name="versions">The runtime versions.</param>
    /// <param name="responses">The response service.</param>
    /// <param name="shapes">The runner shapes.</param>
    /// <param name="identities">The respondent identity factory.</param>
    /// <param name="displayManager">The display manager of responses.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="serviceProvider">The services, for the optional survey authorization service.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public SurveyResponsesController(
        SurveyStore surveyStore,
        SurveyResponseStore responseStore,
        SurveyCollectorStore collectorStore,
        ISurveyVersionCache versionCache,
        SurveyRuntimeVersions versions,
        ISurveyResponseService responses,
        SurveyRunnerShapes shapes,
        SurveyRespondentIdentityFactory identities,
        IDisplayManager<SurveyResponse> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        ISession session,
        INotifier notifier,
        IServiceProvider serviceProvider,
        IStringLocalizer<SurveyResponsesController> stringLocalizer,
        IHtmlLocalizer<SurveyResponsesController> htmlLocalizer)
    {
        _surveyStore = surveyStore;
        _responseStore = responseStore;
        _collectorStore = collectorStore;
        _versionCache = versionCache;
        _versions = versions;
        _responses = responses;
        _shapes = shapes;
        _identities = identities;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _session = session;
        _notifier = notifier;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the responses of a survey.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="options">The filters.</param>
    /// <param name="pagerParameters">The pager.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/responses", "SurveysResponses")]
    public async Task<IActionResult> Index(string id, SurveyResponsesIndexViewModel options, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        var survey = await _surveyStore.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.ViewResponses))
        {
            return Forbid();
        }

        options ??= new SurveyResponsesIndexViewModel();
        options.Survey = survey;

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var surveyId = survey.ItemId;
        var query = _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.ResponsesCollectionName);

        if (Enum.TryParse<SurveyResponseStatus>(options.Status, out var status))
        {
            var statusName = status.ToString();
            query = query.Where(index => index.Status == statusName);
        }

        if (int.TryParse(options.Version, out var versionNumber))
        {
            query = query.Where(index => index.VersionNumber == versionNumber);
        }

        if (!string.IsNullOrEmpty(options.Collector))
        {
            var collectorId = options.Collector;
            query = query.Where(index => index.CollectorId == collectorId);
        }

        if (!options.IncludeTest)
        {
            query = query.Where(index => !index.IsTest);
        }

        var anonymous = SurveyAnonymity.IsAnonymous(survey);
        IReadOnlyList<string> labelMatches = anonymous && !string.IsNullOrWhiteSpace(options.Search)
            ? await _serviceProvider.GetRequiredService<SurveyRespondentNames>().FindByLabelAsync(survey, options.Search, HttpContext.RequestAborted)
            : [];

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            query = Search(query, options.Search, anonymous, labelMatches);
        }

        if (options.UpkeepStopped)
        {
            query = query.Where(index => index.UpkeepFailures >= SurveyMaintenanceService.MaxUpkeepFailures);
        }

        IEnumerable<SurveyResponse> items;
        var count = 0;

        // The shared small-group rule (SurveyGroupGate), counted on the narrowing filters (version, collector, search,
        // upkeep) without the status filter: nothing lists until the completed, non-test responses reach the minimum
        // group size, and a status too few real responses share is left out (all of the list when it is the status
        // filtered to), so filtering to Abandoned can't single out the one person who gave up.
        //
        // A response whose joint key (collector, version, context kind) is in a group too small to show isn't listed, as
        // Details refuses it (SurveyAnonymousAttributes); it leaves the query, so the count and pages leave it out. With a
        // Collector or Version filter, a masked response (shown as "Other") leaves too, or the filter would tell its real
        // collector or version (SurveyAnonymousGroups.ConcealedGroupsAsync). The status counts leave out the same groups.
        var filtersStatus = Enum.TryParse<SurveyResponseStatus>(options.Status, out var shown);
        var narrowing = new SurveyResultsScope
        {
            CollectorId = options.Collector,
            VersionNumbers = int.TryParse(options.Version, out _) ? [versionNumber] : [],
        };

        IReadOnlyList<SurveyResponseGroup> concealed = anonymous
            ? await _serviceProvider.GetRequiredService<SurveyAnonymousGroups>().ConcealedGroupsAsync(survey, narrowing, HttpContext.RequestAborted)
            : [];

        SurveyGroupDecision decision;

        if (concealed is null)
        {
            decision = new SurveyGroupDecision { MinimumGroupSize = await _serviceProvider.GetRequiredService<SurveyAnonymousGroups>().MinimumGroupSizeAsync(survey), HidesAll = true };
        }
        else
        {
            decision = await SurveyGroupGate.DecideAsync(survey, await SettingsAsync(), filtersStatus ? [shown] : [], async other => await CountRealAsync(surveyId, versionNumber, options, other, anonymous, labelMatches, concealed));
            query = query.Excluding(concealed);
        }

        if (decision.HidesAll)
        {
            options.HiddenBelow = decision.MinimumGroupSize;
            items = [];
        }
        else
        {
            if (decision.HiddenStatuses.Count > 0)
            {
                var hidden = decision.HiddenStatuses.Select(value => value.ToString()).ToArray();

                // Test rows are previews by staff, not people, so they stay.
                query = query.Where(index => index.Status.IsNotIn(hidden) || index.IsTest);
                options.HidesSmallStatusGroups = true;
            }

            count = await query.CountAsync();

            // Response numbers and start times order the responses of the whole site, so an anonymous survey lists by
            // its random ids instead, which order nothing.
            items = anonymous
                ? await query.OrderBy(index => index.ItemId).Skip(pager.GetStartIndex()).Take(pager.PageSize).ListAsync()
                : await query.OrderByDescending(index => index.StartedUtc).Skip(pager.GetStartIndex()).Take(pager.PageSize).ListAsync();
        }

        options.UpkeepStoppedCount = await CountUpkeepStoppedAsync(surveyId);
        // The permission is checked first, so it never depends on the stuck count.
        var canRetryUpkeep = await AuthorizeAsync(survey, SurveyOperation.RetryUpkeep);
        options.CanRetryUpkeep = canRetryUpkeep && options.UpkeepStoppedCount > 0;

        options.StatusOptions =
        [
            new SelectListItem(S["All statuses"], string.Empty, string.IsNullOrEmpty(options.Status)),
            new SelectListItem(S["In progress"], nameof(SurveyResponseStatus.InProgress), options.Status == nameof(SurveyResponseStatus.InProgress)),
            new SelectListItem(S["Completed"], nameof(SurveyResponseStatus.Completed), options.Status == nameof(SurveyResponseStatus.Completed)),
            new SelectListItem(S["Screened out"], nameof(SurveyResponseStatus.ScreenedOut), options.Status == nameof(SurveyResponseStatus.ScreenedOut)),
            new SelectListItem(S["Abandoned"], nameof(SurveyResponseStatus.Abandoned), options.Status == nameof(SurveyResponseStatus.Abandoned)),
            new SelectListItem(S["Voided"], nameof(SurveyResponseStatus.Voided), options.Status == nameof(SurveyResponseStatus.Voided)),
        ];

        options.VersionOptions = [new SelectListItem(S["All versions"], string.Empty, string.IsNullOrEmpty(options.Version))];

        // The version numbers come from the version index: the filter never loads the versions' definitions.
        foreach (var published in await _serviceProvider.GetRequiredService<SurveyVersionStore>().ListNumbersAsync(survey.ItemId, HttpContext.RequestAborted))
        {
            var number = published.ToString(System.Globalization.CultureInfo.InvariantCulture);
            options.VersionOptions.Add(new SelectListItem(S["Version {0}", published], number, options.Version == number));
        }

        options.CollectorOptions = [new SelectListItem(S["All collectors"], string.Empty, string.IsNullOrEmpty(options.Collector))];

        foreach (var collector in await _collectorStore.ListBySurveyAsync(survey.ItemId))
        {
            options.CollectorOptions.Add(new SelectListItem(collector.Name, collector.ItemId, options.Collector == collector.ItemId));
        }

        if (await AuthorizeAsync(survey, SurveyOperation.DeleteResponses))
        {
            options.BulkActions =
            [
                new SelectListItem(S["Void"], VoidAction),
                new SelectListItem(S["Delete"], DeleteAction),
            ];

            // Each row offers Void and Delete only when the user may remove that response.
            var rowActions = _serviceProvider.GetRequiredService<SurveyResponseRowActions>();

            foreach (var item in items)
            {
                if (await MayRemoveAsync(survey, item))
                {
                    rowActions.RemovableIds.Add(item.ItemId);
                }
            }
        }

        var routeData = new RouteData();
        routeData.Values[nameof(options.Status)] = options.Status;
        routeData.Values[nameof(options.Version)] = options.Version;
        routeData.Values[nameof(options.Collector)] = options.Collector;
        routeData.Values[nameof(options.IncludeTest)] = options.IncludeTest;
        routeData.Values[nameof(options.Search)] = options.Search;
        routeData.Values[nameof(options.UpkeepStopped)] = options.UpkeepStopped;

        options.List = new AdminListContext<SurveyResponse>
        {
            Source = items,
            Pager = (IShape)await _shapeFactory.PagerAsync(pager, count, routeData),
        };

        return View(options);
    }

    /// <summary>
    /// Applies a bulk action to responses.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="options">The filters and the bulk action.</param>
    /// <param name="itemIds">The responses.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("surveys/{id}/responses", "SurveysResponses")]
    public async Task<IActionResult> IndexBulk(string id, SurveyResponsesIndexViewModel options, IEnumerable<string> itemIds)
    {
        var survey = await _surveyStore.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.DeleteResponses))
        {
            return Forbid();
        }

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var done = 0;
        var refused = 0;

        foreach (var responseId in itemIds?.Take(SurveysConstants.PageSize) ?? [])
        {
            var response = await _responseStore.FindByIdAsync(responseId);

            // Nobody voids or deletes their own response, which would give a failed attempt back; a given grade changes
            // only for whoever may change scores.
            if (response is null || response.SurveyId != survey.ItemId)
            {
                continue;
            }

            if (!await MayRemoveAsync(survey, response))
            {
                refused++;

                continue;
            }

            var result = options?.BulkAction switch
            {
                VoidAction => await _responses.VoidAsync(responseId, actor, S["Voided from the responses list."]),
                DeleteAction => await _responses.DeleteAsync(responseId, actor),
                _ => null,
            };

            if (result?.Succeeded == true)
            {
                done++;
            }
        }

        // The notice counts only the responses that changed, and says how many were left alone and why.
        await _notifier.SuccessAsync(H["{0} responses were changed.", done]);

        if (refused > 0)
        {
            await _notifier.WarningAsync(H["{0} responses were left as they were: they are your own, or they carry a grade you aren't allowed to change.", refused]);
        }

        return RedirectToAction(nameof(Index), new { id });
    }

    /// <summary>
    /// Filters the list.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="options">The filters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("surveys/{id}/responses", "SurveysResponses")]
    public IActionResult IndexFilter(string id, SurveyResponsesIndexViewModel options)
        => RedirectToAction(nameof(Index), new
        {
            id,
            options?.Status,
            options?.Version,
            options?.Collector,
            options?.IncludeTest,
            options?.Search,
            options?.UpkeepStopped,
        });

    /// <summary>
    /// Shows one response with every detail.
    /// </summary>
    /// <param name="id">The response.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/responses/{id}", "SurveysResponse")]
    public async Task<IActionResult> Details(string id)
    {
        var response = await _responseStore.FindByIdAsync(id);
        var survey = response is null ? null : await _surveyStore.FindByIdAsync(response.SurveyId);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.ViewResponses))
        {
            return Forbid();
        }

        // One response of an anonymous survey opens under the list's rule (SurveyGroupGate): only once the survey's
        // completed responses reach its minimum group size, and only when enough responses share its status, so the one
        // abandoned response can't be read by its id. Its collector, version and context kind are a group too, decided
        // on their joint key (SurveyAnonymousAttributes): a response given through a link sent to one person, or to a
        // version only one person answered through a link, doesn't open on its own. Both refusals give one notice (the
        // Grade page's too), so a refusal doesn't tell which of the response's groups is small.
        var attributes = SurveyRowAttributes.All;

        if (!response.IsTest && SurveyAnonymity.IsAnonymous(survey))
        {
            var groups = _serviceProvider.GetRequiredService<SurveyAnonymousGroups>();

            if (!await groups.ShowsAsync(survey, response, HttpContext.RequestAborted))
            {
                await _notifier.WarningAsync(H["This survey is anonymous, and too few of its responses are like this one. It is hidden so no one can be recognized (the minimum is {0}).", await groups.MinimumGroupSizeAsync(survey)]);

                return RedirectToAction(nameof(Index), new { id = survey.ItemId });
            }

            attributes = (await groups.AttributesAsync(survey, HttpContext.RequestAborted)).Of(response);
        }

        var version = await _versions.GetAsync(survey, response);

        if (version?.Definition is null)
        {
            return NotFound();
        }

        var model = new SurveyResponseDetailsViewModel
        {
            Survey = survey,
            Version = version,
            Response = response,
            Label = await _serviceProvider.GetRequiredService<SurveyRespondentNames>().GetLabelAsync(survey, response, HttpContext.RequestAborted),
            ShowsVersion = attributes.ShowsVersion,
            Questions = await _shapes.BuildReviewAsync(survey, version, response, ReviewShows.All),
            Extensions = await _displayManager.BuildDisplayAsync(response, _updateModelAccessor.ModelUpdater, "DetailAdmin"),
            // Void and Delete show only when the user may use them on this response.
            CanDelete = await AuthorizeAsync(survey, SurveyOperation.DeleteResponses) && await MayRemoveAsync(survey, response),
        };

        foreach (var page in version.Definition.Pages)
        {
            model.PageNames[page.Id] = string.IsNullOrWhiteSpace(page.Title) ? page.Name : page.Title;
        }

        foreach (var category in version.Definition.ScoreCategories)
        {
            model.CategoryNames[category.Id] = category.Title ?? category.Name;
        }

        return View(model);
    }

    /// <summary>
    /// Voids a response.
    /// </summary>
    /// <param name="id">The response.</param>
    /// <param name="reason">Why.</param>
    /// <returns>A redirect to the response.</returns>
    [HttpPost]
    [Admin("surveys/responses/{id}/void", "SurveysResponseVoid")]
    public async Task<IActionResult> Void(string id, string reason)
    {
        var response = await _responseStore.FindByIdAsync(id);
        var survey = response is null ? null : await _surveyStore.FindByIdAsync(response.SurveyId);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.DeleteResponses))
        {
            return Forbid();
        }

        if (SurveyOwnWork.IsOwn(User, response))
        {
            await _notifier.ErrorAsync(H["You can't void your own response. Someone else must."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        if (!await MayRemoveAsync(survey, response))
        {
            return Forbid();
        }

        var result = await _responses.VoidAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), reason);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The response was voided."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The response could not be voided."]);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Deletes a response.
    /// </summary>
    /// <param name="id">The response.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("surveys/responses/{id}/delete", "SurveysResponseDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var response = await _responseStore.FindByIdAsync(id);
        var survey = response is null ? null : await _surveyStore.FindByIdAsync(response.SurveyId);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.DeleteResponses))
        {
            return Forbid();
        }

        if (SurveyOwnWork.IsOwn(User, response))
        {
            await _notifier.ErrorAsync(H["You can't delete your own response. Someone else must."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        if (!await MayRemoveAsync(survey, response))
        {
            return Forbid();
        }

        await _responses.DeleteAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier));
        await _notifier.SuccessAsync(H["The response was deleted."]);

        return RedirectToAction(nameof(Index), new { id = survey.ItemId });
    }

    /// <summary>
    /// Has background upkeep try again the responses of a survey it stopped trying after too many failures in a row.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="responses">The response service, which clears the counts under each response's lock.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("surveys/{id}/responses/retry-upkeep", "SurveysResponsesRetryUpkeep")]
    public async Task<IActionResult> RetryUpkeep(string id, [FromServices] SurveyResponseService responses)
    {
        var survey = await _surveyStore.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.RetryUpkeep))
        {
            return Forbid();
        }

        var surveyId = survey.ItemId;
        var stopped = await _session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.UpkeepFailures >= SurveyMaintenanceService.MaxUpkeepFailures, collection: SurveysConstants.ResponsesCollectionName)
            .Take(SurveysConstants.PageSize)
            .ListAsync(HttpContext.RequestAborted);
        var cleared = 0;

        foreach (var row in stopped)
        {
            var result = await responses.ClearUpkeepFailuresAsync(row.ItemId, HttpContext.RequestAborted);

            // A response being changed right now is left for another try: after a refused save the session takes no more.
            if (result.Error == SurveyErrorCode.Busy)
            {
                break;
            }

            if (result.Succeeded)
            {
                cleared++;
            }
        }

        await _notifier.SuccessAsync(H["Background upkeep will try {0} responses again on its next run.", cleared]);

        return RedirectToAction(nameof(Index), new { id });
    }

    /// <summary>
    /// Shows the page that starts a preview: it posts the start at once (or with its button), so opening the Preview
    /// link alone creates no test response.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="version">Draft to run the draft, anything else to run the published version.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("surveys/{id}/preview", "SurveysPreview")]
    public async Task<IActionResult> Preview(string id, string version = "draft")
    {
        var survey = await _surveyStore.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.Manage))
        {
            return Forbid();
        }

        return View("PreviewStart", new SurveyPreviewStartViewModel
        {
            SurveyId = survey.ItemId,
            Title = survey.Title,
            Version = string.Equals(version, "published", StringComparison.OrdinalIgnoreCase) ? "published" : "draft",
        });
    }

    /// <summary>
    /// Previews a survey: runs its draft (or its published version) as a test response, left out of results and limits.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="version">Draft to run the draft, anything else to run the published version.</param>
    /// <returns>A redirect to the runner in preview mode.</returns>
    [HttpPost]
    [ActionName(nameof(Preview))]
    [ValidateAntiForgeryToken]
    [Admin("surveys/{id}/preview")]
    public async Task<IActionResult> PreviewPost(string id, string version = "draft")
    {
        var survey = await _surveyStore.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.Manage))
        {
            return Forbid();
        }

        var identity = _identities.Build(survey.ItemId);

        identity.IsStaff = true;
        identity.ResumeToken = null;

        var result = await _responses.StartAsync(new StartResponseRequest
        {
            SurveyId = survey.ItemId,
            Identity = identity,
            IsTest = true,
            UseDraft = !string.Equals(version, "published", StringComparison.OrdinalIgnoreCase),
        });

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["The survey cannot be previewed: it has no page or no published version."]);

            return RedirectToAction(nameof(Index), new { id });
        }

        if (!string.IsNullOrEmpty(result.ResumeToken))
        {
            _identities.WriteResumeCookie(survey.ItemId, result.ResumeToken);
        }

        return Redirect(Url.Action(nameof(SurveyRunnerController.Respond), "SurveyRunner", new { area = SurveysConstants.Features.Surveys, responseId = result.Response.ItemId, preview = true }));
    }

    /// <summary>
    /// Moves a preview response to a page, to check that page without answering the ones before.
    /// </summary>
    /// <param name="id">The test response.</param>
    /// <param name="screen">The page to go to.</param>
    /// <returns>A redirect to the runner.</returns>
    [HttpPost]
    [Admin("surveys/responses/{id}/jump", "SurveysPreviewJump")]
    public async Task<IActionResult> Jump(string id, string screen)
    {
        var response = await _responseStore.FindByIdAsync(id);
        var survey = response is null ? null : await _surveyStore.FindByIdAsync(response.SurveyId);

        if (survey is null || !response.IsTest || response.Status != SurveyResponseStatus.InProgress || !response.Plan.PageOrder.Contains(screen))
        {
            return NotFound();
        }

        if (!await AuthorizeAsync(survey, SurveyOperation.Manage))
        {
            return Forbid();
        }

        response.CurrentPageId = screen;
        response.PagePath.Add(screen);
        await _responseStore.UpdateAsync(response);

        return Redirect(Url.Action(nameof(SurveyRunnerController.Respond), "SurveyRunner", new { area = SurveysConstants.Features.Surveys, responseId = id, preview = true }));
    }

    private async Task<SurveySettings> SettingsAsync()
        => _serviceProvider.GetService<ISiteService>() is { } siteService ? await siteService.GetSettingsAsync<SurveySettings>() : null;

    private async Task<int> CountRealAsync(string surveyId, int versionNumber, SurveyResponsesIndexViewModel options, SurveyResponseStatus status, bool anonymous, IReadOnlyList<string> labelMatches, IReadOnlyList<SurveyResponseGroup> concealed)
    {
        var statusName = status.ToString();
        var query = _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId && index.Status == statusName && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName);

        if (int.TryParse(options.Version, out _))
        {
            query = query.Where(index => index.VersionNumber == versionNumber);
        }

        if (!string.IsNullOrEmpty(options.Collector))
        {
            var collectorId = options.Collector;
            query = query.Where(index => index.CollectorId == collectorId);
        }

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            query = Search(query, options.Search, anonymous, labelMatches);
        }

        if (options.UpkeepStopped)
        {
            query = query.Where(index => index.UpkeepFailures >= SurveyMaintenanceService.MaxUpkeepFailures);
        }

        // The groups the list leaves out don't count towards a status either, or a status would show on responses the
        // list can't list.
        return await query.Excluding(concealed).CountAsync(HttpContext.RequestAborted);
    }

    // An anonymous survey shows no response number (see SurveyAnonymity.ResponseLabel), so it can't be searched by one
    // either: searching another survey's number would tell whether that person's next response is here. It searches the
    // label it shows, a keyed hash of the id, matched whole within the survey (labelMatches, from
    // SurveyRespondentNames.FindByLabelAsync).
    private static IQuery<SurveyResponse, SurveyResponseIndex> Search(IQuery<SurveyResponse, SurveyResponseIndex> query, string text, bool anonymous, IReadOnlyList<string> labelMatches)
    {
        var search = text.Trim();

        if (anonymous)
        {
            var ids = labelMatches is { Count: > 0 } ? labelMatches.ToArray() : [string.Empty];

            return query.Where(index => index.ItemId.IsIn(ids));
        }

        return query.Where(index => index.Number.Contains(search));
    }

    private Task<int> CountUpkeepStoppedAsync(string surveyId)
        => _session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.UpkeepFailures >= SurveyMaintenanceService.MaxUpkeepFailures, collection: SurveysConstants.ResponsesCollectionName)
            .CountAsync(HttpContext.RequestAborted);

    // Nobody voids or deletes their own response, which would give a failed attempt back. Voiding or deleting a
    // completed, scored response changes a given grade: every score change check must allow it (with Learning
    // Assessments, Override grades on a survey a course item owns).
    private async Task<bool> MayRemoveAsync(Survey survey, SurveyResponse response)
        => !SurveyOwnWork.IsOwn(User, response)
            && (!SurveyScoreChangeChecks.RemovingChangesGivenScore(response)
            || _serviceProvider.GetService<SurveyScoreChangeChecks>() is not { } checks
            || await checks.AllowAsync(User, survey, response, HttpContext.RequestAborted));

    private async Task<bool> AuthorizeAsync(Survey survey, SurveyOperation operation)
    {
        if (_serviceProvider.GetService<ISurveyAuthorizationService>() is { } surveys)
        {
            return await surveys.AuthorizeAsync(User, survey, operation, HttpContext.RequestAborted);
        }

        var own = !string.IsNullOrEmpty(survey.OwnerId) && survey.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier);

        return operation switch
        {
            SurveyOperation.ViewResponses => await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ViewSurveyResponses)
                || (own && await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ViewOwnSurveyResponses)),
            SurveyOperation.DeleteResponses or SurveyOperation.RetryUpkeep => await _authorizationService.AuthorizeAsync(User, SurveysPermissions.DeleteSurveyResponses),
            _ => await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveys)
                || (own && await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageOwnSurveys)),
        };
    }

}
