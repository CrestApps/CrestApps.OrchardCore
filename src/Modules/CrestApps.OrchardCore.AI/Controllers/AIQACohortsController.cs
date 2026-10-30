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
/// The QA cohorts: samples of real conversations scored against criteria after they close.
/// </summary>
[Admin]
[Feature(AIConstants.Feature.Quality)]
public sealed class AIQACohortsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIQACohortStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIQACohort> _displayManager;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQACohortsController"/> class.
    /// </summary>
    /// <param name="store">The cohort store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The cohort display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIQACohortsController(
        IAIQACohortStore store,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIQACohort> displayManager,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<AIQACohortsController> htmlLocalizer,
        IStringLocalizer<AIQACohortsController> stringLocalizer)
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
    /// Lists the cohorts.
    /// </summary>
    /// <param name="options">The search.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("ai/qa/cohorts", "AIQACohortsIndex")]
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
            List = new AdminListContext<AIQACohort>
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
    [Admin("ai/qa/cohorts", "AIQACohortsIndex")]
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
    /// Shows the form for a new cohort.
    /// </summary>
    /// <returns>The form.</returns>
    [Admin("ai/qa/cohorts/create", "AIQACohortsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New QA cohort"],
            Editor = await _displayManager.BuildEditorAsync(new AIQACohort(), _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Saves a new cohort. It starts with the conversations that close after it is saved.
    /// </summary>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/qa/cohorts/create", "AIQACohortsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var cohort = new AIQACohort
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = _clock.UtcNow,
            CursorUtc = _clock.UtcNow,
        };

        var editor = await _displayManager.UpdateEditorAsync(cohort, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = S["New QA cohort"], Editor = editor });
        }

        await _store.CreateAsync(cohort);
        await _notifier.SuccessAsync(H["The QA cohort was created. It scores the conversations that close from now on."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows a cohort's form.
    /// </summary>
    /// <param name="id">The cohort.</param>
    /// <returns>The form.</returns>
    [Admin("ai/qa/cohorts/edit/{id}", "AIQACohortsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var cohort = await _store.FindByIdAsync(id);

        if (cohort is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = cohort.Name,
            Editor = await _displayManager.BuildEditorAsync(cohort, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a cohort.
    /// </summary>
    /// <param name="id">The cohort.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/qa/cohorts/edit/{id}", "AIQACohortsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var cohort = await _store.FindByIdAsync(id);

        if (cohort is null)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(cohort, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = cohort.Name, Editor = editor });
        }

        await _store.UpdateAsync(cohort);
        await _notifier.SuccessAsync(H["The QA cohort was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a cohort. Its evaluations are kept.
    /// </summary>
    /// <param name="id">The cohort.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("ai/qa/cohorts/delete/{id}", "AIQACohortsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        var cohort = await _store.FindByIdAsync(id);

        if (cohort is not null)
        {
            await _store.DeleteAsync(cohort);
            await _notifier.SuccessAsync(H["The QA cohort was deleted. Its evaluations are kept."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
