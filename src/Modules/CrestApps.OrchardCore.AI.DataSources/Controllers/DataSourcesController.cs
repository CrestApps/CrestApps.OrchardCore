using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.DataSources.Controllers;

/// <summary>
/// Provides endpoints for managing data sources resources.
/// </summary>
[Feature(AIConstants.Feature.DataSources)]
public sealed class DataSourcesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ICatalogManager<AIDataSource> _dataSourceManager;
    private readonly IDisplayManager<AIDataSource> _displayManager;
    private readonly IAIDataSourceIndexingQueue _indexingQueue;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourcesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="dataSourceManager">The data source manager.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="indexingQueue">The indexing queue.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataSourcesController(
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        ICatalogManager<AIDataSource> dataSourceManager,
        IDisplayManager<AIDataSource> displayManager,
        IAIDataSourceIndexingQueue indexingQueue,
        INotifier notifier,
        IHtmlLocalizer<DataSourcesController> htmlLocalizer,
        IStringLocalizer<DataSourcesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _dataSourceManager = dataSourceManager;
        _displayManager = displayManager;
        _indexingQueue = indexingQueue;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Performs the index operation.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("ai/data-sources", "AIDataSourcesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _dataSourceManager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Sorted = true,
            Name = options.Search,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<AIDataSource>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var record in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<AIDataSource>
            {
                Model = record,
                Shape = await _displayManager.BuildDisplayAsync(record, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    /// <summary>
    /// Performs the index filter post operation.
    /// </summary>
    /// <param name="model">The model.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/data-sources", "AIDataSourcesIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Creates a new .
    /// </summary>
    [Admin("ai/data-source/create", "AIDataSourceCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var dataSource = await _dataSourceManager.NewAsync();

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Data Source"],
            Editor = await _displayManager.BuildEditorAsync(dataSource, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    /// <summary>
    /// Creates a new post.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/data-source/create", "AIDataSourceCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var dataSource = await _dataSourceManager.NewAsync();

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Data Source"],
            Editor = await _displayManager.UpdateEditorAsync(dataSource, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _dataSourceManager.CreateAsync(dataSource);

            await _notifier.SuccessAsync(H["Data source has been created successfully. Index synchronization is running in the background to populate the AI Knowledge Base index."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Performs the edit operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [Admin("ai/data-source/edit/{id}", "AIDataSourceEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var deployment = await _dataSourceManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = deployment.DisplayText,
            Editor = await _displayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    /// <summary>
    /// Performs the edit post operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/data-source/edit/{id}", "AIDataSourceEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var deployment = await _dataSourceManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = deployment.DisplayText,
            Editor = await _displayManager.UpdateEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _dataSourceManager.UpdateAsync(deployment);

            await _notifier.SuccessAsync(H["Data source has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Removes the .
    /// </summary>
    /// <param name="id">The id.</param>
    [HttpPost]
    [Admin("ai/data-source/delete/{id}", "AIDataSourceDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var deployment = await _dataSourceManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        await _dataSourceManager.DeleteAsync(deployment);

        await _notifier.SuccessAsync(H["Data source has been deleted successfully."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Performs the sync index operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [HttpPost]
    [Admin("ai/data-source/sync-index/{id}", "AIDataSourceSyncIndex")]
    public async Task<IActionResult> SyncIndex(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var dataSource = await _dataSourceManager.FindByIdAsync(id);

        if (dataSource == null)
        {
            return NotFound();
        }

        await _indexingQueue.QueueSyncDataSourceAsync(dataSource);

        await _notifier.SuccessAsync(H["The data source index synchronization has been triggered in the background."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Performs the index post operation.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="itemIds">The item ids.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/data-sources", "AIDataSourcesIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
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
                        var dataSource = await _dataSourceManager.FindByIdAsync(id);

                        if (dataSource == null)
                        {
                            continue;
                        }

                        if (await _dataSourceManager.DeleteAsync(dataSource))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No data sources were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 data source has been removed successfully.", "{0} data sources have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
