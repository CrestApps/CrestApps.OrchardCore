using System.Security.Claims;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The survey list, new surveys, copies, status changes, the access and settings page and the logic overview.
/// </summary>
[Admin]
public sealed class SurveysController : Controller
{
    private const string ShowAll = "all";

    private readonly ISurveyManager _manager;
    private readonly SurveyListService _listService;
    private readonly ISurveyAuthorizationService _surveyAuthorization;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<Survey> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IEnumerable<ISurveyTemplateProvider> _templateProviders;
    private readonly ISurveyDefinitionValidator _validator;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysController"/> class.
    /// </summary>
    public SurveysController(
        ISurveyManager manager,
        SurveyListService listService,
        ISurveyAuthorizationService surveyAuthorization,
        IAuthorizationService authorizationService,
        IDisplayManager<Survey> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IEnumerable<ISurveyTemplateProvider> templateProviders,
        ISurveyDefinitionValidator validator,
        INotifier notifier,
        IHtmlLocalizer<SurveysController> htmlLocalizer,
        IStringLocalizer<SurveysController> stringLocalizer)
    {
        _manager = manager;
        _listService = listService;
        _surveyAuthorization = surveyAuthorization;
        _authorizationService = authorizationService;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _templateProviders = templateProviders;
        _validator = validator;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the surveys.
    /// </summary>
    /// <param name="options">The search and filters.</param>
    /// <param name="pagerParameters">The pager.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("surveys", "SurveysIndex")]
    public async Task<IActionResult> Index(
        SurveyListOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageOwnSurveys))
        {
            return Forbid();
        }

        options ??= new SurveyListOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var manageAll = await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveys);
        var (count, items) = await _listService.ListAsync(new SurveyListQuery
        {
            Search = options.Search,
            Kind = options.Kind,
            Status = options.Status,
            Tag = options.Tag,
            IncludeOwned = options.Show == ShowAll,
            OwnerId = manageAll ? null : CurrentUserId(),
        }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        var routeData = new RouteData();

        AddRouteValue(routeData, "Options.Search", options.Search);
        AddRouteValue(routeData, "Options.Kind", options.Kind);
        AddRouteValue(routeData, "Options.Status", options.Status);
        AddRouteValue(routeData, "Options.Tag", options.Tag);
        AddRouteValue(routeData, "Options.Show", options.Show);

        var model = new SurveyListViewModel
        {
            Options = options,
            List = new AdminListContext<Survey>
            {
                Source = items,
                Pager = await shapeFactory.PagerAsync(pager, count, routeData),
            },
        };

        var filters = new List<AdminListFilter>
        {
            new("Options.Kind",
            [
                new SelectListItem(S["All kinds"], string.Empty, string.IsNullOrEmpty(options.Kind)),
                new SelectListItem(S["Surveys"], SurveyKinds.Survey, options.Kind == SurveyKinds.Survey),
                new SelectListItem(S["Quizzes"], SurveyKinds.Quiz, options.Kind == SurveyKinds.Quiz),
                new SelectListItem(S["Polls"], SurveyKinds.Poll, options.Kind == SurveyKinds.Poll),
            ], S["Filter by kind"], S["Kind"]),
            new("Options.Status",
            [
                new SelectListItem(S["Not archived"], string.Empty, string.IsNullOrEmpty(options.Status)),
                new SelectListItem(S["Draft"], nameof(SurveyStatus.Draft), options.Status == nameof(SurveyStatus.Draft)),
                new SelectListItem(S["Open"], nameof(SurveyStatus.Open), options.Status == nameof(SurveyStatus.Open)),
                new SelectListItem(S["Closed"], nameof(SurveyStatus.Closed), options.Status == nameof(SurveyStatus.Closed)),
                new SelectListItem(S["Archived"], nameof(SurveyStatus.Archived), options.Status == nameof(SurveyStatus.Archived)),
            ], S["Filter by status"], S["Status"]),
            new("Options.Show",
            [
                new SelectListItem(S["Standalone surveys"], string.Empty, options.Show != ShowAll),
                new SelectListItem(S["Also surveys owned by other features"], ShowAll, options.Show == ShowAll),
            ], S["Show"], S["Show"]),
        };

        var tags = await _listService.ListTagsAsync(HttpContext.RequestAborted);

        if (tags.Count > 0)
        {
            filters.Add(new AdminListFilter(
                "Options.Tag",
                [new SelectListItem(S["All tags"], string.Empty, string.IsNullOrEmpty(options.Tag)), .. tags.Select(tag => new SelectListItem(tag, tag, tag == options.Tag))],
                S["Filter by tag"],
                S["Tag"]));
        }

        model.List.ToolbarActions = await shapeFactory.CreateAsync<AdminListFiltersViewModel>("SurveysAdminListFilters", filtersModel =>
        {
            filtersModel.ListName = "Surveys";
            filtersModel.FormId = "surveys-filter";
            filtersModel.Filters = filters;
        });

        return View(model);
    }

    /// <summary>
    /// Applies the list's search and filters.
    /// </summary>
    /// <param name="model">The posted list.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("surveys", "SurveysIndex")]
    public IActionResult IndexFilterPost(SurveyListViewModel model)
    {
        var options = model?.Options ?? new SurveyListOptions();
        var values = new RouteValueDictionary();

        AddRouteValue(values, "Options.Search", options.Search);
        AddRouteValue(values, "Options.Kind", options.Kind);
        AddRouteValue(values, "Options.Status", options.Status);
        AddRouteValue(values, "Options.Tag", options.Tag);
        AddRouteValue(values, "Options.Show", options.Show);

        return RedirectToAction(nameof(Index), values);
    }

    /// <summary>
    /// Shows the new survey form.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("surveys/create", "SurveysCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageOwnSurveys))
        {
            return Forbid();
        }

        return View(new SurveyCreateViewModel { Templates = await TemplatesAsync() });
    }

    /// <summary>
    /// Creates a survey and opens its designer.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <returns>A redirect to the designer.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("surveys/create", "SurveysCreate")]
    public async Task<IActionResult> CreatePost(SurveyCreateViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageOwnSurveys))
        {
            return Forbid();
        }

        var templates = await TemplatesAsync();
        var template = templates.FirstOrDefault(item => item.Name == model.Template);

        if (template is null)
        {
            ModelState.AddModelError(nameof(model.Template), S["Pick a template."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Name) && !SurveyNames.IsValidName(model.Name.Trim()))
        {
            ModelState.AddModelError(nameof(model.Name), S["The technical name may hold only lowercase letters, digits and dashes."]);
        }
        else if (!string.IsNullOrWhiteSpace(model.Name) && await _manager.FindByNameAsync(model.Name.Trim()) is not null)
        {
            ModelState.AddModelError(nameof(model.Name), S["Another survey uses this technical name."]);
        }

        if (!ModelState.IsValid)
        {
            model.Templates = templates;

            return View(model);
        }

        var survey = await _manager.CreateSurveyAsync(new SurveyCreateRequest
        {
            Title = model.Title.Trim(),
            Name = model.Name?.Trim(),
            Kind = template.Kind,
            Template = template.Name,
        }, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["The survey has been created."]);

        return RedirectToAction("Design", "SurveyDesigner", new { id = survey.ItemId });
    }

    /// <summary>
    /// Copies a survey as a new draft.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>A redirect to the copy's designer.</returns>
    [HttpPost]
    [Admin("surveys/{id}/duplicate", "SurveysDuplicate")]
    public async Task<IActionResult> Duplicate(string id)
    {
        var survey = await _manager.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted)
            || !await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageOwnSurveys))
        {
            return Forbid();
        }

        var copy = await _manager.DuplicateAsync(id, cancellationToken: HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["The survey has been copied as a draft."]);

        return RedirectToAction("Design", "SurveyDesigner", new { id = copy.ItemId });
    }

    /// <summary>
    /// Opens, closes, archives or unarchives a survey.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="status">The new status.</param>
    /// <param name="returnUrl">Where to go back to.</param>
    /// <returns>A redirect.</returns>
    [HttpPost]
    [Admin("surveys/{id}/status", "SurveysStatus")]
    public async Task<IActionResult> Status(string id, SurveyStatus status, string returnUrl = null)
    {
        var survey = await _manager.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Publish, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await _manager.ChangeStatusAsync(id, status, HttpContext.RequestAborted);

        if (result == SurveyErrorCode.None)
        {
            await _notifier.SuccessAsync(status switch
            {
                SurveyStatus.Open => H["The survey is open."],
                SurveyStatus.Closed => H["The survey is closed."],
                SurveyStatus.Archived => H["The survey is archived."],
                _ => H["The survey has been updated."],
            });
        }
        else
        {
            await _notifier.ErrorAsync(H["The survey cannot move to that status."]);
        }

        return LocalRedirect(returnUrl, nameof(Index));
    }

    /// <summary>
    /// Deletes a survey that has no responses.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("surveys/{id}/delete", "SurveysDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var survey = await _manager.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await _manager.DeleteSurveyAsync(id, HttpContext.RequestAborted);

        if (result == SurveyErrorCode.HasResponses)
        {
            await _notifier.WarningAsync(H["The survey has responses and cannot be deleted. Archive it instead."]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The survey has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the access and settings page.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/access", "SurveysAccess")]
    public async Task<IActionResult> Access(string id)
    {
        var survey = await _manager.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        return View(new SurveyEditorPageViewModel
        {
            Survey = survey,
            Editor = await _displayManager.BuildEditorAsync(survey, _updateModelAccessor.ModelUpdater, false, SurveyDetailsDisplayDriver.GroupId),
        });
    }

    /// <summary>
    /// Saves the access and settings page.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="auditTrail">The audit trail.</param>
    /// <returns>The page, or a redirect.</returns>
    [HttpPost]
    [ActionName(nameof(Access))]
    [Admin("surveys/{id}/access", "SurveysAccess")]
    public async Task<IActionResult> AccessPost(string id, [FromServices] ISurveyAuditTrail auditTrail)
    {
        var survey = await _manager.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var editor = await _displayManager.UpdateEditorAsync(survey, _updateModelAccessor.ModelUpdater, false, SurveyDetailsDisplayDriver.GroupId);

        if (!ModelState.IsValid)
        {
            return View(new SurveyEditorPageViewModel { Survey = survey, Editor = editor });
        }

        await _manager.UpdateAsync(survey, null, HttpContext.RequestAborted);
        await auditTrail.RecordAsync(SurveyAuditEvents.AccessChanged, new SurveyAuditEntry
        {
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            SurveyId = survey.ItemId,
            SurveyName = survey.Name,
        }, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The settings have been saved."]);

        return RedirectToAction(nameof(Access), new { id });
    }

    /// <summary>
    /// Shows the logic of a survey's draft in plain words.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/logic", "SurveysLogic")]
    public async Task<IActionResult> Logic(string id)
    {
        var survey = await _manager.FindByIdAsync(id);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var definition = survey.Draft ?? new SurveyDefinition();
        var describer = new SurveyRuleDescriber(definition, S);
        var model = new SurveyLogicViewModel { Survey = survey };

        for (var i = 0; i < definition.Pages.Count; i++)
        {
            var page = definition.Pages[i];
            var logic = new SurveyLogicPageViewModel { Title = string.IsNullOrWhiteSpace(page.Title) ? S["Page {0}", i + 1] : page.Title };

            if (page.Visibility is { Mode: not VisibilityMode.Always } visibility)
            {
                logic.Lines.Add(visibility.Mode == VisibilityMode.ShowWhen
                    ? S["The page shows when {0}.", describer.Describe(visibility.Rule)]
                    : S["The page hides when {0}.", describer.Describe(visibility.Rule)]);
            }

            foreach (var question in page.Questions.Where(question => question.Visibility is { Mode: not VisibilityMode.Always }))
            {
                var title = string.IsNullOrWhiteSpace(question.Title) ? question.Name : question.Title;

                logic.Lines.Add(question.Visibility.Mode == VisibilityMode.ShowWhen
                    ? S["“{0}” shows when {1}.", title, describer.Describe(question.Visibility.Rule)]
                    : S["“{0}” hides when {1}.", title, describer.Describe(question.Visibility.Rule)]);
            }

            foreach (var branch in page.Branches)
            {
                var target = branch.TargetKind == BranchTargetKind.Ending
                    ? definition.Endings.FirstOrDefault(ending => ending.Id == branch.TargetId)?.Title ?? S["an ending"]
                    : definition.Pages.FirstOrDefault(other => other.Id == branch.TargetId)?.Title ?? S["a later page"];

                logic.Lines.Add(S["When {0}, the survey goes to {1}.", describer.Describe(branch.Rule), target]);
            }

            if (logic.Lines.Count == 0)
            {
                logic.Lines.Add(S["Every question shows, and the next page follows."]);
            }

            model.Pages.Add(logic);
        }

        foreach (var ending in definition.Endings)
        {
            var title = string.IsNullOrWhiteSpace(ending.Title) ? ending.Name : ending.Title;

            model.Endings.Add(ending.IsDefault
                ? S["“{0}” is the default ending.", title]
                : S["“{0}” ends the survey when {1}.", title, describer.Describe(ending.Condition?.Rule)]);
        }

        model.Problems = (await _validator.ValidateAsync(survey, definition, HttpContext.RequestAborted)).Messages;

        return View(model);
    }

    private async Task<List<SurveyTemplateDescriptor>> TemplatesAsync()
    {
        var templates = new List<SurveyTemplateDescriptor>();

        foreach (var provider in _templateProviders)
        {
            templates.AddRange(await provider.GetTemplatesAsync(HttpContext.RequestAborted));
        }

        return templates
            .OrderBy(template => template.Position)
            .ThenBy(template => template.Title, StringComparer.CurrentCulture)
            .ToList();
    }

    private string CurrentUserId()
        => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private IActionResult LocalRedirect(string returnUrl, string fallbackAction)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(fallbackAction);

    private static void AddRouteValue(RouteData routeData, string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            routeData.Values.TryAdd(key, value);
        }
    }

    private static void AddRouteValue(RouteValueDictionary values, string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            values.TryAdd(key, value);
        }
    }
}
