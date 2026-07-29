using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.BackgroundJobs;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Provides endpoints for managing activity batches resources.
/// </summary>
[Admin]
public sealed class ActivityBatchesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ICatalogManager<OmnichannelActivityBatch> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<OmnichannelActivityBatch> _batchDisplayDriver;
    private readonly INotifier _notifier;
    private readonly ActivityBatchSourceOptions _activityBatchSourceOptions;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityBatchesController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="batchDisplayManager">The batch display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="activityBatchSourceOptions">The configured activity batch sources.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivityBatchesController(
        ICatalogManager<OmnichannelActivityBatch> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OmnichannelActivityBatch> batchDisplayManager,
        INotifier notifier,
        IOptions<ActivityBatchSourceOptions> activityBatchSourceOptions,
        IHtmlLocalizer<ActivityBatchesController> htmlLocalizer,
        IStringLocalizer<ActivityBatchesController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _batchDisplayDriver = batchDisplayManager;
        _notifier = notifier;
        _activityBatchSourceOptions = activityBatchSourceOptions.Value;
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
    [Admin("omnichannel/activity/batches", "OmnichannelActivityBatchesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Name = options.Search,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListOmnichannelActivityBatchViewModel
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            Sources = _activityBatchSourceOptions.Sources.Values
                .Where(source => source.ShowInCreationPicker)
                .OrderBy(source => source.DisplayName.Value),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<OmnichannelActivityBatch>
            {
                Model = model,
                Shape = await _batchDisplayDriver.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        viewModel.Options.BulkActions = [];

        return View(viewModel);
    }

    /// <summary>
    /// Performs the index filter post operation.
    /// </summary>
    /// <param name="model">The model.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/activity/batches", "OmnichannelActivityBatchesIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
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
    [Admin("omnichannel/activity/batches/create/{source}", "OmnichannelActivityBatchesCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        if (!TryGetActivityBatchSource(source, out var sourceEntry))
        {
            await _notifier.ErrorAsync(H["Unable to find an inventory load source with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var model = await _manager.NewAsync();
        model.Source = sourceEntry.Source;

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = sourceEntry.DisplayName,
            Editor = await _batchDisplayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Creates a new post.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/activity/batches/create/{source}", "OmnichannelActivityBatchesCreate")]
    public async Task<ActionResult> CreatePost(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        if (!TryGetActivityBatchSource(source, out var sourceEntry))
        {
            await _notifier.ErrorAsync(H["Unable to find an inventory load source with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var model = await _manager.NewAsync();
        model.Source = sourceEntry.Source;

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = sourceEntry.DisplayName,
            Editor = await _batchDisplayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(model);

            await _notifier.SuccessAsync(H["A new inventory load has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    /// <summary>
    /// Performs the edit operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [Admin("omnichannel/activity/batches/edit/{id}", "OmnichannelActivityBatchesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _batchDisplayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        ViewData["IsReadOnly"] = model.Status != OmnichannelActivityBatchStatus.New;

        return View(viewModel);
    }

    /// <summary>
    /// Performs the edit post operation.
    /// </summary>
    /// <param name="id">The id.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/activity/batches/edit/{id}", "OmnichannelActivityBatchesEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.Status == OmnichannelActivityBatchStatus.Loaded)
        {
            await _notifier.ErrorAsync(H["This batch was already loaded and can't be edited."]);

            return RedirectToAction(nameof(Index));
        }

        if (model.Status == OmnichannelActivityBatchStatus.Started || model.Status == OmnichannelActivityBatchStatus.Loading)
        {
            await _notifier.ErrorAsync(H["This batch is being loaded and can't be edited."]);

            return RedirectToAction(nameof(Index));
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _batchDisplayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);

            await _notifier.SuccessAsync(H["The inventory load has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        ViewData["IsReadOnly"] = model.Status != OmnichannelActivityBatchStatus.New;

        return View(viewModel);
    }

    /// <summary>
    /// Removes the .
    /// </summary>
    /// <param name="id">The id.</param>
    [HttpPost]
    [Admin("omnichannel/activity/batches/delete/{id}", "OmnichannelActivityBatchesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.Status == OmnichannelActivityBatchStatus.Loaded)
        {
            await _notifier.ErrorAsync(H["This batch was already loaded and can't be removed."]);

            return RedirectToAction(nameof(Index));
        }

        if (model.Status == OmnichannelActivityBatchStatus.Started || model.Status == OmnichannelActivityBatchStatus.Loading)
        {
            await _notifier.ErrorAsync(H["This batch is being loaded and can't be removed."]);

            return RedirectToAction(nameof(Index));
        }

        if (await _manager.DeleteAsync(model))
        {
            await _notifier.SuccessAsync(H["The inventory load has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the inventory load."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Loads the .
    /// </summary>
    /// <param name="id">The id.</param>
    [HttpPost]
    [Admin("omnichannel/activity/batches/load/{id}", "OmnichannelActivityBatchesLoad")]
    public async Task<ActionResult> Load(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.Status == OmnichannelActivityBatchStatus.Loaded)
        {
            await _notifier.ErrorAsync(H["This batch was already loaded and can't be loaded again."]);

            return RedirectToAction(nameof(Index));
        }

        if (model.Status == OmnichannelActivityBatchStatus.Started || model.Status == OmnichannelActivityBatchStatus.Loading)
        {
            await _notifier.ErrorAsync(H["This batch was already being loaded and can't be loaded again."]);

            return RedirectToAction(nameof(Index));
        }

        model.Status = OmnichannelActivityBatchStatus.Started;
        await _manager.UpdateAsync(model);

        var loaderId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var loaderUserName = User.Identity.Name;
        var batchId = model.ItemId;

        ShellScope.AddDeferredTask(async s =>
        {
            // Resolve the loader that matches the batch source and let it query the contacts,
            // apply the source-specific filters, and create the activities in the background.
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("load-activity-batch", loaderId, loaderUserName, batchId, async (scope, userId, userName, id) =>
            {
                var coordinator = scope.ServiceProvider.GetRequiredService<IActivityBatchLoadCoordinator>();

                await coordinator.LoadAsync(id, userId, userName);
            });
        });

        await _notifier.SuccessAsync(H["The inventory load has started loading in the background."]);

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
    [Admin("omnichannel/activity/batches", "OmnichannelActivityBatchesIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageActivityBatches))
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
                        var instance = await _manager.FindByIdAsync(id);

                        if (instance == null)
                        {
                            continue;
                        }

                        if (await _manager.DeleteAsync(instance))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No inventory loads were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 inventory load has been removed successfully.", "{0} inventory loads have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    private bool TryGetActivityBatchSource(string source, out ActivityBatchSourceEntry sourceEntry)
        => TryGetActivityBatchSource(source, _activityBatchSourceOptions, out sourceEntry);

    private static bool TryGetActivityBatchSource(string source, ActivityBatchSourceOptions options, out ActivityBatchSourceEntry sourceEntry)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            sourceEntry = null;

            return false;
        }

        var normalizedSource = source.Trim();

        return options.Sources.TryGetValue(normalizedSource, out sourceEntry);
    }
}
