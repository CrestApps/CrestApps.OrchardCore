using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// The AI alert rules: metrics of the AI conversations checked against thresholds.
/// </summary>
[Admin]
[Feature(AIConstants.Feature.Quality)]
public sealed class AIAlertRulesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIAlertRuleStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIAlertRule> _displayManager;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIAlertRulesController"/> class.
    /// </summary>
    /// <param name="store">The rule store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The rule display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIAlertRulesController(
        IAIAlertRuleStore store,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIAlertRule> displayManager,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<AIAlertRulesController> htmlLocalizer,
        IStringLocalizer<AIAlertRulesController> stringLocalizer)
    {
        _store = store;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the alert rules.
    /// </summary>
    /// <param name="options">The search.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("ai/alerts", "AIAlertRulesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await _store.PageAsync(pager.Page, pager.PageSize, new QueryContext { Name = options.Search });
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var pagerShape = await shapeFactory.PagerAsync(pager, result.Count, routeData);

        return View(new ListCatalogEntryViewModel
        {
            Options = options,
            List = new AdminListContext<AIAlertRule>
            {
                Source = result.Entries,
                Pager = pagerShape,
            },
        });
    }

    /// <summary>
    /// Applies the search.
    /// </summary>
    /// <param name="model">The submitted list.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/alerts", "AIAlertRulesIndex")]
    public async Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Shows the form for a new rule.
    /// </summary>
    /// <returns>The form.</returns>
    [Admin("ai/alerts/create", "AIAlertRulesCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New alert rule"],
            Editor = await _displayManager.BuildEditorAsync(new AIAlertRule(), _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Saves a new alert rule.
    /// </summary>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/alerts/create", "AIAlertRulesCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var rule = new AIAlertRule
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = _clock.UtcNow,
        };

        var editor = await _displayManager.UpdateEditorAsync(rule, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = S["New alert rule"], Editor = editor });
        }

        await _store.CreateAsync(rule);
        await _notifier.SuccessAsync(H["The alert rule was created. It is checked within a minute."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows a rule's form.
    /// </summary>
    /// <param name="id">The rule.</param>
    /// <returns>The form.</returns>
    [Admin("ai/alerts/edit/{id}", "AIAlertRulesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var rule = await _store.FindByIdAsync(id);

        if (rule is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = rule.Name,
            Editor = await _displayManager.BuildEditorAsync(rule, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a rule.
    /// </summary>
    /// <param name="id">The rule.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/alerts/edit/{id}", "AIAlertRulesEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var rule = await _store.FindByIdAsync(id);

        if (rule is null)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(rule, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = rule.Name, Editor = editor });
        }

        await _store.UpdateAsync(rule);
        await _notifier.SuccessAsync(H["The alert rule was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes an alert rule.
    /// </summary>
    /// <param name="id">The rule.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("ai/alerts/delete/{id}", "AIAlertRulesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var rule = await _store.FindByIdAsync(id);

        if (rule is not null)
        {
            await _store.DeleteAsync(rule);
            await _notifier.SuccessAsync(H["The alert rule was deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
