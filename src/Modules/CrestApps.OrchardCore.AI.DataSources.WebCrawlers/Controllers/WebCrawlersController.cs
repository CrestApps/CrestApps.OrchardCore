using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Services;
using CrestApps.OrchardCore.Core.Models;
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
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Controllers;

/// <summary>
/// Manages web crawlers in the admin area. Each crawl strategy (for example <c>Sitemap</c>) is a source,
/// so the create flow mirrors the other source-based catalog editors (AI Templates, Data Sources).
/// </summary>
public sealed class WebCrawlersController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISourceCatalogManager<WebCrawler> _manager;
    private readonly IDisplayManager<WebCrawler> _displayManager;
    private readonly IWebCrawlerReindexPlanner _reindexPlanner;
    private readonly IReadOnlyList<WebCrawlerStrategyDescriptor> _strategies;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCrawlersController"/> class.
    /// </summary>
    public WebCrawlersController(
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        ISourceCatalogManager<WebCrawler> manager,
        IDisplayManager<WebCrawler> displayManager,
        IWebCrawlerReindexPlanner reindexPlanner,
        IOptions<WebCrawlerStrategyOptions> strategyOptions,
        INotifier notifier,
        IHtmlLocalizer<WebCrawlersController> htmlLocalizer,
        IStringLocalizer<WebCrawlersController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _manager = manager;
        _displayManager = displayManager;
        _reindexPlanner = reindexPlanner;
        _strategies = strategyOptions.Value.Strategies
            .OrderBy(strategy => strategy.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays a paginated list of web crawlers.
    /// </summary>
    [Admin("ai/web-crawlers", "WebCrawlersIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Sorted = true,
            Name = options.Search,
        });

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListSourceModelViewModel<WebCrawlerStrategyDescriptor, CatalogEntryViewModel<WebCrawler>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            Sources = _strategies,
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<WebCrawler>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    /// <summary>
    /// Handles the filter form submission for the web crawlers index page.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/web-crawlers", "WebCrawlersIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the form for creating a new web crawler for the given strategy.
    /// </summary>
    /// <param name="source">The crawl strategy identifier.</param>
    [Admin("ai/web-crawler/create/{source}", "WebCrawlersCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        if (!TryGetStrategy(source, out var strategy))
        {
            await _notifier.ErrorAsync(H["Unable to find a crawl strategy with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var crawler = await _manager.NewAsync(strategy.Strategy);

        if (crawler == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new web crawler."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = strategy.DisplayName.Value,
            Editor = await _displayManager.BuildEditorAsync(crawler, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for creating a new web crawler.
    /// </summary>
    /// <param name="source">The crawl strategy identifier.</param>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/web-crawler/create/{source}", "WebCrawlersCreate")]
    public async Task<ActionResult> CreatePost(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        if (!TryGetStrategy(source, out var strategy))
        {
            await _notifier.ErrorAsync(H["Unable to find a crawl strategy with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var crawler = await _manager.NewAsync(strategy.Strategy);

        if (crawler == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new web crawler."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = strategy.DisplayName.Value,
            Editor = await _displayManager.UpdateEditorAsync(crawler, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(crawler);

            await _notifier.SuccessAsync(H["Web crawler has been created successfully. Initial synchronization has been queued."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Displays the form for editing an existing web crawler.
    /// </summary>
    /// <param name="id">The unique identifier of the crawler to edit.</param>
    [Admin("ai/web-crawler/edit/{id}", "WebCrawlersEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        var crawler = await _manager.FindByIdAsync(id);

        if (crawler == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = crawler.DisplayText,
            Editor = await _displayManager.BuildEditorAsync(crawler, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for editing an existing web crawler.
    /// </summary>
    /// <param name="id">The unique identifier of the crawler to update.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/web-crawler/edit/{id}", "WebCrawlersEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        var crawler = await _manager.FindByIdAsync(id);

        if (crawler == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = crawler.DisplayText,
            Editor = await _displayManager.UpdateEditorAsync(crawler, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(crawler);

            await _notifier.SuccessAsync(H["Web crawler has been updated successfully. Synchronization has been queued."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Deletes a web crawler by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the crawler to delete.</param>
    [HttpPost]
    [Admin("ai/web-crawler/delete/{id}", "WebCrawlersDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        var crawler = await _manager.FindByIdAsync(id);

        if (crawler == null)
        {
            return NotFound();
        }

        if (await _manager.DeleteAsync(crawler))
        {
            await _notifier.SuccessAsync(H["Web crawler has been deleted successfully. Knowledge-base cleanup has been queued."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the web crawler."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Re-crawls just this site and queues the new, changed, and removed pages for indexing.
    /// </summary>
    /// <param name="id">The unique identifier of the crawler to synchronize.</param>
    [HttpPost]
    [Admin("ai/web-crawler/sync/{id}", "WebCrawlersSync")]
    public async Task<IActionResult> Sync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        var crawler = await _manager.FindByIdAsync(id);

        if (crawler == null)
        {
            return NotFound();
        }

        var result = await _reindexPlanner.PlanAndEnqueueAsync(crawler, HttpContext.RequestAborted);

        if (result.Status is WebCrawlerReindexStatus.DiscoveryFailed or WebCrawlerReindexStatus.NoPagesDiscovered)
        {
            await _notifier.ErrorAsync(H["{0}", result.Message]);
        }
        else
        {
            await _notifier.SuccessAsync(H["Web crawler synchronization queued: {0} new, {1} changed, {2} removed.", result.NewCount, result.ChangedCount, result.RemovedCount]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Handles bulk actions on selected web crawlers.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/web-crawlers", "WebCrawlersIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case CatalogEntryAction.None:
                    break;
                case CatalogEntryAction.Remove:
                    var counter = 0;
                    foreach (var id in itemIds)
                    {
                        var crawler = await _manager.FindByIdAsync(id);

                        if (crawler == null)
                        {
                            continue;
                        }

                        if (await _manager.DeleteAsync(crawler))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No web crawlers were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 web crawler has been removed successfully.", "{0} web crawlers have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    private bool TryGetStrategy(string source, out WebCrawlerStrategyDescriptor strategy)
    {
        strategy = null;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        strategy = _strategies.FirstOrDefault(entry =>
            string.Equals(entry.Strategy, source, StringComparison.OrdinalIgnoreCase));

        return strategy != null;
    }
}
